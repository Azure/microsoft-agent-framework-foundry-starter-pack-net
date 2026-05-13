using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MafStarterPack.Agent.Extensions;

/// <summary>
/// A <see cref="DelegatingChatClient"/> that turns a multi-turn replay of full chat history into
/// a chained call against the OpenAI Responses API using <c>previous_response_id</c>.
/// </summary>
/// <remarks>
/// <para>
/// <c>Microsoft.Agents.AI.Hosting.OpenAI</c>'s <c>AIAgentResponseExecutor</c> deliberately does
/// not propagate <c>ChatOptions.ConversationId</c>. Instead it prepends the entire conversation
/// history to each invocation of the underlying <see cref="IChatClient"/>. When the underlying
/// client is <c>OpenAIResponsesChatClient</c> talking to a strict server (e.g.
/// <c>Azure.AI.AgentServer</c> via <c>MapFoundryResponses</c>), the prior assistant turns are
/// serialized as <c>output_text</c> response items in the new request's <c>input</c>; the server
/// rejects them with <c>"Required property 'logprobs' is missing"</c>.
/// </para>
/// <para>
/// On every call this wrapper:
/// <list type="number">
///   <item><description>Derives a stable conversation key from the first message in the
///   prepended history (its <see cref="ChatMessage.MessageId"/> if present, otherwise a hash of
///   its role and text). The first message of a conversation is invariant across turns by
///   definition, so this key is consistent for every turn of the same conversation.</description></item>
///   <item><description>If a prior upstream <c>resp_*</c> id is tracked for that key and the
///   incoming list contains any assistant turn, trims the list to only the messages after the
///   last assistant turn (the new input) and sets <see cref="ChatOptions.ConversationId"/> to
///   the tracked <c>resp_*</c> id. <c>OpenAIResponsesChatClient</c> forwards a non-<c>conv_*</c>
///   ConversationId as <c>previous_response_id</c>.</description></item>
///   <item><description>After the call, records the new upstream <see cref="ChatResponse.ResponseId"/>
///   against the conversation key for the next turn.</description></item>
/// </list>
/// The upstream request therefore contains only the new user input plus a <c>previous_response_id</c>,
/// so no <c>output_text</c> items appear in <c>input</c> and the validation error cannot fire.
/// </para>
/// <para>
/// The hosting layer (<c>AgentResponseUpdateExtensions.ToStreamingResponseAsync</c>) regenerates
/// its own <c>msg_*</c> ids via its <c>IdGenerator</c>, so correlating by assistant
/// <see cref="ChatMessage.MessageId"/> would never match across turns. Correlating by the
/// invariant first message of the conversation avoids that problem.
/// </para>
/// </remarks>
internal sealed class PreviousResponseIdChatClient : DelegatingChatClient
{
    private readonly ConcurrentDictionary<string, string> _conversationKeyToResponseId = new(StringComparer.Ordinal);
    private readonly ILogger _logger;

    public PreviousResponseIdChatClient(IChatClient innerClient, ILogger<PreviousResponseIdChatClient>? logger = null)
        : base(innerClient)
    {
        _logger = (ILogger?)logger ?? NullLogger.Instance;
    }

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var (rewrittenMessages, conversationKey, previousResponseId) = Rewrite(messages);
        var effectiveOptions = ApplyPreviousResponseId(options, previousResponseId);

        var response = await base.GetResponseAsync(rewrittenMessages, effectiveOptions, cancellationToken)
                                 .ConfigureAwait(false);

        if (conversationKey is not null && response.ResponseId is { Length: > 0 } responseId)
        {
            _conversationKeyToResponseId[conversationKey] = responseId;
            _logger.LogInformation("PreviousResponseIdChatClient: stored upstream response id {ResponseId} for conversation key {ConversationKey}.", responseId, conversationKey);
        }

        return response;
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var (rewrittenMessages, conversationKey, previousResponseId) = Rewrite(messages);
        var effectiveOptions = ApplyPreviousResponseId(options, previousResponseId);

        string? upstreamResponseId = null;

        await foreach (var update in base.GetStreamingResponseAsync(rewrittenMessages, effectiveOptions, cancellationToken)
                                         .ConfigureAwait(false))
        {
            upstreamResponseId ??= update.ResponseId;
            yield return update;
        }

        if (conversationKey is not null && upstreamResponseId is { Length: > 0 })
        {
            _conversationKeyToResponseId[conversationKey] = upstreamResponseId;
            _logger.LogInformation("PreviousResponseIdChatClient: stored upstream response id {ResponseId} for conversation key {ConversationKey} (streaming).", upstreamResponseId, conversationKey);
        }
    }

    private (IEnumerable<ChatMessage> Messages, string? ConversationKey, string? PreviousResponseId) Rewrite(IEnumerable<ChatMessage> messages)
    {
        var list = messages as IList<ChatMessage> ?? messages.ToList();
        if (list.Count == 0)
        {
            return (list, null, null);
        }

        var conversationKey = GetConversationKey(list[0]);
        if (conversationKey is null)
        {
            _logger.LogWarning("PreviousResponseIdChatClient: could not derive conversation key from first message (role {Role}, text length {Length}).", list[0].Role, list[0].Text?.Length ?? 0);
            return (list, null, null);
        }

        if (!_conversationKeyToResponseId.TryGetValue(conversationKey, out var previousResponseId))
        {
            _logger.LogInformation("PreviousResponseIdChatClient: no prior response id tracked for conversation key {ConversationKey} (message count {Count}); forwarding as-is.", conversationKey, list.Count);
            // First turn for this conversation; let the inner client send the full input as-is.
            return (list, conversationKey, null);
        }

        // Find the last assistant turn in the prepended history; everything after it is the
        // new input for this turn (typically a single user message, possibly with tool results).
        int lastAssistantIndex = -1;
        for (int i = list.Count - 1; i >= 0; i--)
        {
            if (list[i].Role == ChatRole.Assistant)
            {
                lastAssistantIndex = i;
                break;
            }
        }

        if (lastAssistantIndex < 0)
        {
            // No assistant turn in the prepended history yet; the upstream call still chains
            // on previous_response_id but we send every message we got.
            _logger.LogInformation("PreviousResponseIdChatClient: chaining on {PreviousResponseId} for conversation key {ConversationKey}; no assistant turn in incoming list, forwarding all {Count} messages.", previousResponseId, conversationKey, list.Count);
            return (list, conversationKey, previousResponseId);
        }

        var trimmed = new List<ChatMessage>(list.Count - lastAssistantIndex - 1);
        for (int j = lastAssistantIndex + 1; j < list.Count; j++)
        {
            trimmed.Add(list[j]);
        }

        _logger.LogInformation("PreviousResponseIdChatClient: chaining on {PreviousResponseId} for conversation key {ConversationKey}; trimmed {OriginalCount} messages to {TrimmedCount} after last assistant turn at index {LastAssistantIndex}.", previousResponseId, conversationKey, list.Count, trimmed.Count, lastAssistantIndex);
        return (trimmed, conversationKey, previousResponseId);
    }

    private static string? GetConversationKey(ChatMessage firstMessage)
    {
        // Always hash on (role, text). Do NOT use ChatMessage.MessageId: the agent hosting layer
        // generates ids for messages it persists in conversation storage, so the SAME first user
        // message arrives without an id on turn 1 (fresh request input) but WITH an id on later
        // turns (replayed from storage). Using the id would produce different keys per turn and
        // break correlation.
        var text = firstMessage.Text;
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var bytes = Encoding.UTF8.GetBytes(firstMessage.Role.Value + "\0" + text);
        var hash = SHA256.HashData(bytes);
        return "hash:" + Convert.ToHexString(hash);
    }

    private static ChatOptions ApplyPreviousResponseId(ChatOptions? options, string? previousResponseId)
    {
        if (previousResponseId is null)
        {
            return options ?? new ChatOptions();
        }

        // Clone to avoid mutating the caller's instance.
        var effective = options is null ? new ChatOptions() : options.Clone();

        // OpenAIResponsesChatClient treats a non-"conv_*" ConversationId as previous_response_id.
        effective.ConversationId = previousResponseId;
        return effective;
    }
}

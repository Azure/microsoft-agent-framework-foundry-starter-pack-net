using System.Diagnostics;
using System.Text;

namespace MafStarterPack.Agent.Extensions;

/// <summary>
/// Debug <see cref="DelegatingHandler"/> that logs the body of each outgoing request and
/// the body of the response when verbose hosted-agent diagnostics are enabled.
/// </summary>
/// <remarks>
/// Intended for development-time troubleshooting of the agent-app -> hosted-agent
/// <c>/v1/responses</c> traffic (e.g. verifying that <c>previous_response_id</c> is sent
/// and the request <c>input</c> only contains the new user message).
/// </remarks>
internal sealed class HostedAgentLoggingHandler : DelegatingHandler
{
    private readonly ILogger<HostedAgentLoggingHandler> _logger;

    public HostedAgentLoggingHandler(ILogger<HostedAgentLoggingHandler> logger)
    {
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var requestBody = await ReadBodyAsync(request.Content, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("HostedAgent → {Method} {Uri}\n{Body}", request.Method, request.RequestUri, requestBody);

        HttpResponseMessage response;
        try
        {
            response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HostedAgent ← failed after {ElapsedMs} ms: {Method} {Uri}", stopwatch.ElapsedMilliseconds, request.Method, request.RequestUri);
            throw;
        }

        // For streaming (SSE) responses we cannot drain the body without breaking it; just log
        // the status and content-type. Otherwise log the buffered body.
        var contentType = response.Content?.Headers.ContentType?.MediaType;
        if (string.Equals(contentType, "text/event-stream", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("HostedAgent ← {Status} {ContentType} (stream) after {ElapsedMs} ms: {Method} {Uri}", (int)response.StatusCode, contentType, stopwatch.ElapsedMilliseconds, request.Method, request.RequestUri);
        }
        else
        {
            var responseBody = response.Content is not null
                ? await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)
                : string.Empty;
            _logger.LogInformation("HostedAgent ← {Status} after {ElapsedMs} ms: {Method} {Uri}\n{Body}", (int)response.StatusCode, stopwatch.ElapsedMilliseconds, request.Method, request.RequestUri, responseBody);
        }

        return response;
    }

    private static async Task<string> ReadBodyAsync(HttpContent? content, CancellationToken cancellationToken)
    {
        if (content is null)
        {
            return "<no body>";
        }

        // Buffering the content lets the body be read again downstream.
        await content.LoadIntoBufferAsync(cancellationToken).ConfigureAwait(false);
        var bytes = await content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        return bytes.Length == 0 ? "<empty body>" : Encoding.UTF8.GetString(bytes);
    }
}

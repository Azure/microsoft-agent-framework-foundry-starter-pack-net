using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var foundry = builder.AddConnectionString("foundry");

var agent = builder.AddProject<MafStarterPack_Agent>("agent")
                   .WithExternalHttpEndpoints()
                   .WithReference(foundry)
                   .WaitFor(foundry);

var webui = builder.AddProject<MafStarterPack_WebUI>("webui")
                    .WithExternalHttpEndpoints()
                    .WithReference(agent)
                    .WaitFor(agent);

await builder.Build().RunAsync();

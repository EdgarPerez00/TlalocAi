using TlalocAi.RaspberryAgent.Application;
using TlalocAi.RaspberryAgent.Infrastructure;
using TlalocAi.RaspberryAgent.Worker;

var builder = Host.CreateApplicationBuilder(args);
var options = new TlalocAgentOptions();
builder.Configuration.Bind(options);

builder.Services.AddRaspberryAgentInfrastructure(options);
builder.Services.AddSingleton<AgentRuntimeState>();
builder.Services.AddHostedService<AgentTelemetryWorker>();
builder.Services.AddHostedService<AgentHeartbeatWorker>();
builder.Services.AddHostedService<AgentCommandWorker>();

await builder.Build().RunAsync();

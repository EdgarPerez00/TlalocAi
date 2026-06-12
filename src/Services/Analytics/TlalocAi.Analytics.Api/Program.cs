using TlalocAi.Analytics.Application;
using TlalocAi.Analytics.Infrastructure;
using TlalocAi.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddTlalocServiceDefaults("TlalocAi.Analytics.Api");
builder.Services.AddAnalyticsInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseTlalocServiceDefaults();

var analytics = app.MapGroup("/api/analytics").WithTags("Analytics").RequireAuthorization();

analytics.MapGet("/summary", (string deviceId, DateTime? fromUtc, DateTime? toUtc, IAnalyticsService service, CancellationToken cancellationToken) =>
    service.GetSummaryAsync(deviceId, fromUtc, toUtc, cancellationToken));

analytics.MapGet("/flow-series", (string deviceId, DateTime? fromUtc, DateTime? toUtc, int? bucketSeconds, IAnalyticsService service, CancellationToken cancellationToken) =>
    service.GetFlowSeriesAsync(deviceId, fromUtc, toUtc, bucketSeconds ?? 60, cancellationToken));

analytics.MapGet("/levels-summary", (string deviceId, DateTime? fromUtc, DateTime? toUtc, IAnalyticsService service, CancellationToken cancellationToken) =>
    service.GetLevelsSummaryAsync(deviceId, fromUtc, toUtc, cancellationToken));

analytics.MapGet("/actuators-summary", (string deviceId, DateTime? fromUtc, DateTime? toUtc, IAnalyticsService service, CancellationToken cancellationToken) =>
    service.GetActuatorsSummaryAsync(deviceId, fromUtc, toUtc, cancellationToken));

analytics.MapGet("/experiments/{experimentId:guid}/summary", (Guid experimentId, IAnalyticsService service, CancellationToken cancellationToken) =>
    service.GetExperimentSummaryAsync(experimentId, cancellationToken));

app.Run();

using Microsoft.AspNetCore.Http.HttpResults;
using TlalocAi.ServiceDefaults;
using TlalocAi.Telemetry.Application;
using TlalocAi.Telemetry.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.AddTlalocServiceDefaults("TlalocAi.Telemetry.Api");
builder.Services.AddTelemetryInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseTlalocServiceDefaults();

var telemetry = app.MapGroup("/api/telemetry").WithTags("Telemetry");

telemetry.MapPost("/batch", async Task<Results<Ok<TelemetryBatchResponse>, ProblemHttpResult>> (
    TelemetryBatchRequest request,
    HttpRequest httpRequest,
    ITelemetryService service,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var headerName = configuration["DeviceAuth:ApiKeyHeaderName"] ?? "X-Device-Api-Key";
    var apiKey = httpRequest.Headers[headerName].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        return TypedResults.Problem("Device API key is required.", statusCode: StatusCodes.Status401Unauthorized);
    }

    var result = await service.StoreBatchAsync(request, apiKey, cancellationToken);
    return result.IsSuccess ? TypedResults.Ok(result.Value!) : TypedResults.Problem(result.Error.Message, statusCode: StatusCodes.Status400BadRequest);
});

telemetry.MapGet("/", (string deviceId, DateTime? fromUtc, DateTime? toUtc, ITelemetryService service, CancellationToken cancellationToken) =>
    service.GetHistoryAsync(deviceId, fromUtc, toUtc, cancellationToken)).RequireAuthorization();

telemetry.MapGet("/latest", async Task<Results<Ok<MeasurementResponse>, ProblemHttpResult>> (
    string deviceId,
    ITelemetryService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.GetLatestAsync(deviceId, cancellationToken);
    return result.IsSuccess ? TypedResults.Ok(result.Value!) : TypedResults.Problem(result.Error.Message, statusCode: StatusCodes.Status404NotFound);
}).RequireAuthorization();

var experiments = app.MapGroup("/api/experiments").WithTags("Experiments").RequireAuthorization();

experiments.MapPost("/", async Task<Results<Created<ExperimentResponse>, ProblemHttpResult>> (
    CreateExperimentRequest request,
    ITelemetryService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.CreateExperimentAsync(request, cancellationToken);
    return result.IsSuccess
        ? TypedResults.Created($"/api/experiments/{result.Value!.Id}", result.Value)
        : TypedResults.Problem(result.Error.Message, statusCode: StatusCodes.Status400BadRequest);
});

experiments.MapGet("/", (string? deviceId, ITelemetryService service, CancellationToken cancellationToken) =>
    service.GetExperimentsAsync(deviceId, cancellationToken));

experiments.MapGet("/{experimentId:guid}", async Task<Results<Ok<ExperimentResponse>, ProblemHttpResult>> (
    Guid experimentId,
    ITelemetryService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.GetExperimentAsync(experimentId, cancellationToken);
    return result.IsSuccess ? TypedResults.Ok(result.Value!) : TypedResults.Problem(result.Error.Message, statusCode: StatusCodes.Status404NotFound);
});

experiments.MapPost("/{experimentId:guid}/finish", async Task<Results<Ok<ExperimentResponse>, ProblemHttpResult>> (
    Guid experimentId,
    ITelemetryService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.FinishExperimentAsync(experimentId, cancellationToken);
    return result.IsSuccess ? TypedResults.Ok(result.Value!) : TypedResults.Problem(result.Error.Message, statusCode: StatusCodes.Status404NotFound);
});

app.Run();

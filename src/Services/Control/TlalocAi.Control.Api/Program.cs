using Microsoft.AspNetCore.Http.HttpResults;
using TlalocAi.Control.Application;
using TlalocAi.Control.Infrastructure;
using TlalocAi.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddTlalocServiceDefaults("TlalocAi.Control.Api");
builder.Services.AddControlInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseTlalocServiceDefaults();

var commands = app.MapGroup("/api/commands").WithTags("Commands");

commands.MapPost("/", async Task<Results<Created<CommandResponse>, ProblemHttpResult>> (
    CreateCommandRequest request,
    IControlService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.CreateCommandAsync(request, cancellationToken);
    return result.IsSuccess
        ? TypedResults.Created($"/api/commands/{result.Value!.Id}", result.Value)
        : TypedResults.Problem(result.Error.Message, statusCode: StatusCodes.Status400BadRequest);
}).RequireAuthorization();

commands.MapGet("/", (string? deviceId, IControlService service, CancellationToken cancellationToken) =>
    service.GetCommandsAsync(deviceId, cancellationToken)).RequireAuthorization();

commands.MapGet("/{commandId:guid}", async Task<Results<Ok<CommandResponse>, ProblemHttpResult>> (
    Guid commandId,
    IControlService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.GetCommandAsync(commandId, cancellationToken);
    return result.IsSuccess ? TypedResults.Ok(result.Value!) : TypedResults.Problem(result.Error.Message, statusCode: StatusCodes.Status404NotFound);
}).RequireAuthorization();

commands.MapPost("/{commandId:guid}/cancel", async Task<Results<Ok<CommandResponse>, ProblemHttpResult>> (
    Guid commandId,
    IControlService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.CancelCommandAsync(commandId, cancellationToken);
    return result.IsSuccess ? TypedResults.Ok(result.Value!) : TypedResults.Problem(result.Error.Message, statusCode: StatusCodes.Status400BadRequest);
}).RequireAuthorization();

commands.MapPost("/{commandId:guid}/ack", async Task<Results<Ok<CommandResponse>, ProblemHttpResult>> (
    Guid commandId,
    AckCommandRequest request,
    HttpRequest httpRequest,
    IControlService service,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var apiKey = httpRequest.Headers[configuration["DeviceAuth:ApiKeyHeaderName"] ?? "X-Device-Api-Key"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        return TypedResults.Problem("Device API key is required.", statusCode: StatusCodes.Status401Unauthorized);
    }

    var result = await service.AckCommandAsync(commandId, request, apiKey, cancellationToken);
    return result.IsSuccess ? TypedResults.Ok(result.Value!) : TypedResults.Problem(result.Error.Message, statusCode: StatusCodes.Status400BadRequest);
});

commands.MapPost("/{commandId:guid}/reject", async Task<Results<Ok<CommandResponse>, ProblemHttpResult>> (
    Guid commandId,
    RejectCommandRequest request,
    HttpRequest httpRequest,
    IControlService service,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var apiKey = httpRequest.Headers[configuration["DeviceAuth:ApiKeyHeaderName"] ?? "X-Device-Api-Key"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        return TypedResults.Problem("Device API key is required.", statusCode: StatusCodes.Status401Unauthorized);
    }

    var result = await service.RejectCommandAsync(commandId, request, apiKey, cancellationToken);
    return result.IsSuccess ? TypedResults.Ok(result.Value!) : TypedResults.Problem(result.Error.Message, statusCode: StatusCodes.Status400BadRequest);
});

var devices = app.MapGroup("/api/devices").WithTags("Device commands");

devices.MapGet("/{deviceId}/commands/pending", async Task<Results<Ok<IReadOnlyList<PendingCommandResponse>>, ProblemHttpResult>> (
    string deviceId,
    HttpRequest httpRequest,
    IControlService service,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var apiKey = httpRequest.Headers[configuration["DeviceAuth:ApiKeyHeaderName"] ?? "X-Device-Api-Key"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        return TypedResults.Problem("Device API key is required.", statusCode: StatusCodes.Status401Unauthorized);
    }

    var result = await service.GetPendingCommandsAsync(deviceId, apiKey, cancellationToken);
    return result.IsSuccess ? TypedResults.Ok(result.Value!) : TypedResults.Problem(result.Error.Message, statusCode: StatusCodes.Status401Unauthorized);
});

devices.MapPost("/{deviceId}/commands/{commandId:guid}/ack", async Task<Results<Ok<CommandResponse>, ProblemHttpResult>> (
    string deviceId,
    Guid commandId,
    DeviceCommandAckRequest request,
    HttpRequest httpRequest,
    IControlService service,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var apiKey = httpRequest.Headers[configuration["DeviceAuth:ApiKeyHeaderName"] ?? "X-Device-Api-Key"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        return TypedResults.Problem("Device API key is required.", statusCode: StatusCodes.Status401Unauthorized);
    }

    var result = await service.AckCommandAsync(commandId, new AckCommandRequest(deviceId, request.Success, request.Message, request.ExecutedAtUtc), apiKey, cancellationToken);
    return result.IsSuccess ? TypedResults.Ok(result.Value!) : TypedResults.Problem(result.Error.Message, statusCode: StatusCodes.Status400BadRequest);
});

devices.MapPost("/{deviceId}/commands/{commandId:guid}/reject", async Task<Results<Ok<CommandResponse>, ProblemHttpResult>> (
    string deviceId,
    Guid commandId,
    DeviceCommandRejectRequest request,
    HttpRequest httpRequest,
    IControlService service,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var apiKey = httpRequest.Headers[configuration["DeviceAuth:ApiKeyHeaderName"] ?? "X-Device-Api-Key"].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        return TypedResults.Problem("Device API key is required.", statusCode: StatusCodes.Status401Unauthorized);
    }

    var result = await service.RejectCommandAsync(commandId, new RejectCommandRequest(deviceId, request.Reason, request.ExecutedAtUtc), apiKey, cancellationToken);
    return result.IsSuccess ? TypedResults.Ok(result.Value!) : TypedResults.Problem(result.Error.Message, statusCode: StatusCodes.Status400BadRequest);
});

var valves = app.MapGroup("/api/valves").WithTags("Valve commands").RequireAuthorization();

valves.MapPost("/{valveId:int}/open", async Task<Results<Created<CommandResponse>, ProblemHttpResult>> (
    int valveId,
    DeviceControlCommandRequest request,
    IControlService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.CreateCommandAsync(new CreateCommandRequest(request.DeviceId, "SetActuatorState", $"valve_{valveId}", true, request.RequestedBy, request.Payload), cancellationToken);
    return result.IsSuccess
        ? TypedResults.Created($"/api/commands/{result.Value!.Id}", result.Value)
        : TypedResults.Problem(result.Error.Message, statusCode: StatusCodes.Status400BadRequest);
});

valves.MapPost("/{valveId:int}/close", async Task<Results<Created<CommandResponse>, ProblemHttpResult>> (
    int valveId,
    DeviceControlCommandRequest request,
    IControlService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.CreateCommandAsync(new CreateCommandRequest(request.DeviceId, "SetActuatorState", $"valve_{valveId}", false, request.RequestedBy, request.Payload), cancellationToken);
    return result.IsSuccess
        ? TypedResults.Created($"/api/commands/{result.Value!.Id}", result.Value)
        : TypedResults.Problem(result.Error.Message, statusCode: StatusCodes.Status400BadRequest);
});

var pumps = app.MapGroup("/api/pumps").WithTags("Pump commands").RequireAuthorization();

pumps.MapPost("/{pumpId}/start", async Task<Results<Created<CommandResponse>, ProblemHttpResult>> (
    string pumpId,
    DeviceControlCommandRequest request,
    IControlService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.CreateCommandAsync(new CreateCommandRequest(request.DeviceId, "SetActuatorState", NormalizePumpTarget(pumpId), true, request.RequestedBy, request.Payload), cancellationToken);
    return result.IsSuccess
        ? TypedResults.Created($"/api/commands/{result.Value!.Id}", result.Value)
        : TypedResults.Problem(result.Error.Message, statusCode: StatusCodes.Status400BadRequest);
});

pumps.MapPost("/{pumpId}/stop", async Task<Results<Created<CommandResponse>, ProblemHttpResult>> (
    string pumpId,
    DeviceControlCommandRequest request,
    IControlService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.CreateCommandAsync(new CreateCommandRequest(request.DeviceId, "SetActuatorState", NormalizePumpTarget(pumpId), false, request.RequestedBy, request.Payload), cancellationToken);
    return result.IsSuccess
        ? TypedResults.Created($"/api/commands/{result.Value!.Id}", result.Value)
        : TypedResults.Problem(result.Error.Message, statusCode: StatusCodes.Status400BadRequest);
});

app.Run();

static string NormalizePumpTarget(string pumpId)
{
    var normalized = pumpId.Trim().ToLowerInvariant();
    return normalized switch
    {
        "1" or "tower" or "torre" => "pump_tower",
        "2" or "cistern" or "cisterna" => "pump_cistern",
        "pump" => "pump",
        _ when normalized.StartsWith("pump_", StringComparison.OrdinalIgnoreCase) => normalized,
        _ => $"pump_{normalized}"
    };
}

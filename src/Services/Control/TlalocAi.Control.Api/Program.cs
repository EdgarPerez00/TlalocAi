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

app.Run();

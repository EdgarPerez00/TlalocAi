using Microsoft.AspNetCore.Http.HttpResults;
using TlalocAi.Devices.Application;
using TlalocAi.Devices.Infrastructure;
using TlalocAi.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddTlalocServiceDefaults("TlalocAi.Devices.Api");
builder.Services.AddDevicesInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseTlalocServiceDefaults();
await app.ApplyDatabaseMigrationsAsync<DevicesDbContext>();

var devices = app.MapGroup("/api/devices").WithTags("Devices").RequireAuthorization();

devices.MapPost("/", async Task<Results<Created<DeviceCreatedResponse>, ProblemHttpResult>> (
    CreateDeviceRequest request,
    IDevicesService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.CreateDeviceAsync(request, cancellationToken);
    return result.IsSuccess
        ? TypedResults.Created($"/api/devices/{result.Value!.Device.Id}", result.Value)
        : TypedResults.Problem(result.Error.Message, statusCode: StatusCodes.Status400BadRequest);
});

devices.MapGet("/", (IDevicesService service, CancellationToken cancellationToken) => service.GetDevicesAsync(cancellationToken));

devices.MapGet("/{deviceId}", async Task<Results<Ok<DeviceResponse>, ProblemHttpResult>> (
    string deviceId,
    IDevicesService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.GetDeviceAsync(deviceId, cancellationToken);
    return result.IsSuccess ? TypedResults.Ok(result.Value!) : TypedResults.Problem(result.Error.Message, statusCode: StatusCodes.Status404NotFound);
});

devices.MapPost("/{deviceId}/rotate-api-key", async Task<Results<Ok<RotateApiKeyResponse>, ProblemHttpResult>> (
    string deviceId,
    IDevicesService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.RotateApiKeyAsync(deviceId, cancellationToken);
    return result.IsSuccess ? TypedResults.Ok(result.Value!) : TypedResults.Problem(result.Error.Message, statusCode: StatusCodes.Status404NotFound);
});

devices.MapPost("/{deviceId}/heartbeat", async Task<Results<Ok<DeviceHeartbeatResponse>, ProblemHttpResult>> (
    string deviceId,
    DeviceHeartbeatRequest request,
    HttpRequest httpRequest,
    IDevicesService service,
    IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    var headerName = configuration["DeviceAuth:ApiKeyHeaderName"] ?? "X-Device-Api-Key";
    var apiKey = httpRequest.Headers[headerName].FirstOrDefault();
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        return TypedResults.Problem("Device API key is required.", statusCode: StatusCodes.Status401Unauthorized);
    }

    var observedIp = httpRequest.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim();
    if (string.IsNullOrWhiteSpace(observedIp))
    {
        observedIp = httpRequest.HttpContext.Connection.RemoteIpAddress?.ToString();
    }

    var result = await service.RegisterHeartbeatAsync(deviceId, request, apiKey, observedIp, cancellationToken);
    return result.IsSuccess ? TypedResults.Ok(result.Value!) : TypedResults.Problem(result.Error.Message, statusCode: StatusCodes.Status401Unauthorized);
}).AllowAnonymous();

devices.MapPost("/{deviceId}/sensors", async Task<Results<Created<SensorResponse>, ProblemHttpResult>> (
    string deviceId,
    CreateSensorRequest request,
    IDevicesService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.CreateSensorAsync(deviceId, request, cancellationToken);
    return result.IsSuccess
        ? TypedResults.Created($"/api/devices/{deviceId}/sensors/{result.Value!.Id}", result.Value)
        : TypedResults.Problem(result.Error.Message, statusCode: StatusCodes.Status400BadRequest);
});

devices.MapGet("/{deviceId}/sensors", (string deviceId, IDevicesService service, CancellationToken cancellationToken) =>
    service.GetSensorsAsync(deviceId, cancellationToken));

devices.MapPost("/{deviceId}/actuators", async Task<Results<Created<ActuatorResponse>, ProblemHttpResult>> (
    string deviceId,
    CreateActuatorRequest request,
    IDevicesService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.CreateActuatorAsync(deviceId, request, cancellationToken);
    return result.IsSuccess
        ? TypedResults.Created($"/api/devices/{deviceId}/actuators/{result.Value!.Id}", result.Value)
        : TypedResults.Problem(result.Error.Message, statusCode: StatusCodes.Status400BadRequest);
});

devices.MapGet("/{deviceId}/actuators", (string deviceId, IDevicesService service, CancellationToken cancellationToken) =>
    service.GetActuatorsAsync(deviceId, cancellationToken));

app.Run();

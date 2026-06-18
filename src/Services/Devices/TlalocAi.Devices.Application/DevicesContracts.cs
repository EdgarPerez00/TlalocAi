using TlalocAi.SharedKernel;

namespace TlalocAi.Devices.Application;

public sealed record CreateDeviceRequest(string Id, string Name, string? Description);
public sealed record DeviceResponse(
    string Id,
    string Name,
    string? Description,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? LastSeenAtUtc,
    string? ObservedPublicIpAddress = null,
    string? Hostname = null,
    string? AgentVersion = null);
public sealed record DeviceCreatedResponse(DeviceResponse Device, string ApiKey);
public sealed record RotateApiKeyResponse(string DeviceId, string ApiKey);
public sealed record DeviceHeartbeatRequest(string? Hostname, string? AgentVersion, DateTime? SentAtUtc);
public sealed record DeviceHeartbeatResponse(string DeviceId, DateTime LastSeenAtUtc, string? ObservedPublicIpAddress, string? Hostname, string? AgentVersion);

public sealed record CreateSensorRequest(string Name, string Type, int GpioPin);
public sealed record SensorResponse(Guid Id, string DeviceId, string Name, string Type, int GpioPin, bool IsActive, DateTime CreatedAtUtc);

public sealed record CreateActuatorRequest(string Name, string Type, int GpioPin, bool ActiveLow);
public sealed record ActuatorResponse(Guid Id, string DeviceId, string Name, string Type, int GpioPin, bool ActiveLow, bool IsActive, DateTime CreatedAtUtc);

public interface IDevicesService
{
    Task<Result<DeviceCreatedResponse>> CreateDeviceAsync(CreateDeviceRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<DeviceResponse>> GetDevicesAsync(CancellationToken cancellationToken);
    Task<Result<DeviceResponse>> GetDeviceAsync(string deviceId, CancellationToken cancellationToken);
    Task<Result<DeviceResponse>> DeleteDeviceAsync(string deviceId, CancellationToken cancellationToken);
    Task<Result<RotateApiKeyResponse>> RotateApiKeyAsync(string deviceId, CancellationToken cancellationToken);
    Task<Result<DeviceHeartbeatResponse>> RegisterHeartbeatAsync(string deviceId, DeviceHeartbeatRequest request, string apiKey, string? observedPublicIpAddress, CancellationToken cancellationToken);
    Task<Result<SensorResponse>> CreateSensorAsync(string deviceId, CreateSensorRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<SensorResponse>> GetSensorsAsync(string deviceId, CancellationToken cancellationToken);
    Task<Result<ActuatorResponse>> CreateActuatorAsync(string deviceId, CreateActuatorRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<ActuatorResponse>> GetActuatorsAsync(string deviceId, CancellationToken cancellationToken);
    Task<bool> ValidateApiKeyAsync(string deviceId, string apiKey, CancellationToken cancellationToken);
}

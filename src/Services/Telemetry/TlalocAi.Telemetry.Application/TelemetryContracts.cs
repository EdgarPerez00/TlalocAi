using TlalocAi.SharedKernel;

namespace TlalocAi.Telemetry.Application;

public sealed record TelemetryBatchRequest(string DeviceId, DateTime SentAtUtc, IReadOnlyList<TelemetryMeasurementRequest> Measurements);
public sealed record TelemetryMeasurementRequest(DateTime TimestampUtc, decimal FlowLpm, decimal TotalLiters, bool PumpOn, IReadOnlyList<LevelRequest> Levels, IReadOnlyList<ActuatorSnapshotRequest> Actuators, Guid? ExperimentId = null);
public sealed record LevelRequest(string Name, bool IsActive);
public sealed record ActuatorSnapshotRequest(string Name, bool IsOn);
public sealed record TelemetryBatchResponse(bool Accepted, int Received, int Stored, string Message);
public sealed record MeasurementResponse(Guid Id, string DeviceId, Guid? ExperimentId, DateTime TimestampUtc, decimal FlowLpm, decimal TotalLiters, bool PumpOn, IReadOnlyList<LevelResponse> Levels, IReadOnlyList<ActuatorSnapshotResponse> Actuators, string? DetailedStateJson = null);
public sealed record LevelResponse(string Name, bool IsActive);
public sealed record ActuatorSnapshotResponse(string Name, bool IsOn);

public sealed record DeviceTelemetryRequest(
    DateTime TimestampUtc,
    ReservoirTelemetryState Tower,
    ReservoirTelemetryState Cistern,
    FlowTelemetryState Flow,
    IReadOnlyList<PumpTelemetryState> Pumps,
    IReadOnlyList<ValveTelemetryState> Valves,
    IReadOnlyList<ContainerTelemetryState> Containers,
    IReadOnlyList<string> Faults,
    IReadOnlyList<string> Warnings,
    Dictionary<string, bool>? RawInputs = null);

public sealed record ReservoirTelemetryState(string Name, int Level, IReadOnlyList<bool> Sensors, bool IsCritical, bool HasInvalidReading, string? Message = null);
public sealed record FlowTelemetryState(decimal LitersPerMinute, decimal TotalLiters, long Pulses, bool NoFlowAlert);
public sealed record PumpTelemetryState(string PumpId, bool IsOn, bool IsBlocked, string? BlockReason);
public sealed record ValveTelemetryState(int ValveId, bool IsOpen, bool IsLocked, string? LockReason);
public sealed record ContainerTelemetryState(int ContainerId, bool IsFull);

public sealed record DeviceStateResponse(
    string DeviceId,
    DateTime TimestampUtc,
    DateTime? LastHeartbeatAtUtc,
    string? ObservedPublicIpAddress,
    string? Hostname,
    string? AgentVersion,
    ReservoirTelemetryState Tower,
    ReservoirTelemetryState Cistern,
    FlowTelemetryState Flow,
    IReadOnlyList<PumpTelemetryState> Pumps,
    IReadOnlyList<ValveTelemetryState> Valves,
    IReadOnlyList<ContainerTelemetryState> Containers,
    IReadOnlyList<string> Faults,
    IReadOnlyList<string> Warnings,
    Dictionary<string, bool>? RawInputs = null);

public sealed record CreateExperimentRequest(string DeviceId, string Name, string? Description, DateTime? StartedAtUtc);
public sealed record ExperimentResponse(Guid Id, string DeviceId, string Name, string? Description, DateTime StartedAtUtc, DateTime? EndedAtUtc, string Status, DateTime CreatedAtUtc);

public interface ITelemetryService
{
    Task<Result<TelemetryBatchResponse>> StoreBatchAsync(TelemetryBatchRequest request, string apiKey, CancellationToken cancellationToken);
    Task<Result<TelemetryBatchResponse>> StoreDeviceTelemetryAsync(string deviceId, DeviceTelemetryRequest request, string apiKey, CancellationToken cancellationToken);
    Task<IReadOnlyList<MeasurementResponse>> GetHistoryAsync(string deviceId, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken);
    Task<Result<MeasurementResponse>> GetLatestAsync(string deviceId, CancellationToken cancellationToken);
    Task<Result<DeviceStateResponse>> GetDeviceStateAsync(string deviceId, CancellationToken cancellationToken);
    Task<Result<ExperimentResponse>> CreateExperimentAsync(CreateExperimentRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExperimentResponse>> GetExperimentsAsync(string? deviceId, CancellationToken cancellationToken);
    Task<Result<ExperimentResponse>> GetExperimentAsync(Guid experimentId, CancellationToken cancellationToken);
    Task<Result<ExperimentResponse>> FinishExperimentAsync(Guid experimentId, CancellationToken cancellationToken);
}

using TlalocAi.SharedKernel;

namespace TlalocAi.Telemetry.Application;

public sealed record TelemetryBatchRequest(string DeviceId, DateTime SentAtUtc, IReadOnlyList<TelemetryMeasurementRequest> Measurements);
public sealed record TelemetryMeasurementRequest(DateTime TimestampUtc, decimal FlowLpm, decimal TotalLiters, bool PumpOn, IReadOnlyList<LevelRequest> Levels, IReadOnlyList<ActuatorSnapshotRequest> Actuators, Guid? ExperimentId = null);
public sealed record LevelRequest(string Name, bool IsActive);
public sealed record ActuatorSnapshotRequest(string Name, bool IsOn);
public sealed record TelemetryBatchResponse(bool Accepted, int Received, int Stored, string Message);
public sealed record MeasurementResponse(Guid Id, string DeviceId, Guid? ExperimentId, DateTime TimestampUtc, decimal FlowLpm, decimal TotalLiters, bool PumpOn, IReadOnlyList<LevelResponse> Levels, IReadOnlyList<ActuatorSnapshotResponse> Actuators);
public sealed record LevelResponse(string Name, bool IsActive);
public sealed record ActuatorSnapshotResponse(string Name, bool IsOn);

public sealed record CreateExperimentRequest(string DeviceId, string Name, string? Description, DateTime? StartedAtUtc);
public sealed record ExperimentResponse(Guid Id, string DeviceId, string Name, string? Description, DateTime StartedAtUtc, DateTime? EndedAtUtc, string Status, DateTime CreatedAtUtc);

public interface ITelemetryService
{
    Task<Result<TelemetryBatchResponse>> StoreBatchAsync(TelemetryBatchRequest request, string apiKey, CancellationToken cancellationToken);
    Task<IReadOnlyList<MeasurementResponse>> GetHistoryAsync(string deviceId, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken);
    Task<Result<MeasurementResponse>> GetLatestAsync(string deviceId, CancellationToken cancellationToken);
    Task<Result<ExperimentResponse>> CreateExperimentAsync(CreateExperimentRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExperimentResponse>> GetExperimentsAsync(string? deviceId, CancellationToken cancellationToken);
    Task<Result<ExperimentResponse>> GetExperimentAsync(Guid experimentId, CancellationToken cancellationToken);
    Task<Result<ExperimentResponse>> FinishExperimentAsync(Guid experimentId, CancellationToken cancellationToken);
}

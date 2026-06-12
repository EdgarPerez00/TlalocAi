namespace TlalocAi.Analytics.Application;

public sealed record AnalyticsSummaryResponse(
    string DeviceId,
    DateTime FromUtc,
    DateTime ToUtc,
    decimal TotalLiters,
    decimal AverageFlowLpm,
    decimal MaxFlowLpm,
    decimal MinFlowLpm,
    int PumpRuntimeSeconds,
    int MeasurementsCount,
    DateTime? LastMeasurementAtUtc,
    IReadOnlyList<ActuatorUsageResponse> Actuators);

public sealed record ActuatorUsageResponse(string Name, int ActiveSeconds, decimal EstimatedLiters);
public sealed record FlowSeriesPoint(DateTime BucketUtc, decimal AverageFlowLpm, int MeasurementsCount);
public sealed record LevelSummaryResponse(string Name, int ActiveCount, int InactiveCount);
public sealed record ActuatorSummaryResponse(string Name, int OnCount, int OffCount, int ActiveSeconds);

public interface IAnalyticsService
{
    Task<AnalyticsSummaryResponse> GetSummaryAsync(string deviceId, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken);
    Task<IReadOnlyList<FlowSeriesPoint>> GetFlowSeriesAsync(string deviceId, DateTime? fromUtc, DateTime? toUtc, int bucketSeconds, CancellationToken cancellationToken);
    Task<IReadOnlyList<LevelSummaryResponse>> GetLevelsSummaryAsync(string deviceId, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken);
    Task<IReadOnlyList<ActuatorSummaryResponse>> GetActuatorsSummaryAsync(string deviceId, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken);
    Task<AnalyticsSummaryResponse> GetExperimentSummaryAsync(Guid experimentId, CancellationToken cancellationToken);
}

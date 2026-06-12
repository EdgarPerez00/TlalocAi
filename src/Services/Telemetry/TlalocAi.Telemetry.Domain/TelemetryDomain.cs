namespace TlalocAi.Telemetry.Domain;

public enum ExperimentStatus
{
    Running = 1,
    Finished = 2,
    Cancelled = 3
}

public sealed class Measurement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string DeviceId { get; set; }
    public Guid? ExperimentId { get; set; }
    public DateTime TimestampUtc { get; set; }
    public decimal FlowLpm { get; set; }
    public decimal TotalLiters { get; set; }
    public bool PumpOn { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<LevelMeasurement> Levels { get; set; } = [];
    public List<ActuatorSnapshot> Actuators { get; set; } = [];
}

public sealed class LevelMeasurement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MeasurementId { get; set; }
    public required string SensorName { get; set; }
    public bool IsActive { get; set; }
}

public sealed class ActuatorSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MeasurementId { get; set; }
    public required string ActuatorName { get; set; }
    public bool IsOn { get; set; }
}

public sealed class Experiment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string DeviceId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
    public ExperimentStatus Status { get; set; } = ExperimentStatus.Running;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

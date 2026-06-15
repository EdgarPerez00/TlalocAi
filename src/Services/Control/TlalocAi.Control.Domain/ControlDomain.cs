namespace TlalocAi.Control.Domain;

public enum DeviceCommandType
{
    SetActuatorState = 1
}

public enum DeviceCommandStatus
{
    Pending = 1,
    Sent = 2,
    Executed = 3,
    Failed = 4,
    Cancelled = 5
}

public sealed class DeviceCommand
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string DeviceId { get; set; }
    public DeviceCommandType Type { get; set; }
    public required string Target { get; set; }
    public bool State { get; set; }
    public string? TargetType { get; set; }
    public string? TargetId { get; set; }
    public string? CommandType { get; set; }
    public string? RequestedBy { get; set; }
    public string? Payload { get; set; }
    public DeviceCommandStatus Status { get; set; } = DeviceCommandStatus.Pending;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? SentAtUtc { get; set; }
    public DateTime? ExecutedAtUtc { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ResultMessage { get; set; }
}

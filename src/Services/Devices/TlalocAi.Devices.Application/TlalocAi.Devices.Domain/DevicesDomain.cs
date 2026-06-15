namespace TlalocAi.Devices.Domain;

public enum SensorType
{
    Flow = 1,
    Level = 2
}

public enum ActuatorType
{
    Pump = 1,
    Valve = 2
}

public sealed class Device
{
    public string Id { get; set; } = string.Empty;
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string ApiKeyHash { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastSeenAtUtc { get; set; }
    public string? ObservedPublicIpAddress { get; set; }
    public string? Hostname { get; set; }
    public string? AgentVersion { get; set; }
    public List<Sensor> Sensors { get; set; } = [];
    public List<Actuator> Actuators { get; set; } = [];
}

public sealed class Sensor
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string DeviceId { get; set; }
    public required string Name { get; set; }
    public SensorType Type { get; set; }
    public int GpioPin { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class Actuator
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string DeviceId { get; set; }
    public required string Name { get; set; }
    public ActuatorType Type { get; set; }
    public int GpioPin { get; set; }
    public bool ActiveLow { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

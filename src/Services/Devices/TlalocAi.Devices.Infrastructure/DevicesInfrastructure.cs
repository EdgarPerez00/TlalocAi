using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TlalocAi.Devices.Application;
using TlalocAi.Devices.Domain;
using TlalocAi.SharedKernel;

namespace TlalocAi.Devices.Infrastructure;

public sealed class DevicesDbContext(DbContextOptions<DevicesDbContext> options) : DbContext(options)
{
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<Sensor> Sensors => Set<Sensor>();
    public DbSet<Actuator> Actuators => Set<Actuator>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Device>(entity =>
        {
            entity.ToTable("devices_devices");
            entity.HasKey(device => device.Id);
            entity.Property(device => device.Id).HasMaxLength(80);
            entity.Property(device => device.Name).HasMaxLength(160).IsRequired();
            entity.Property(device => device.Description).HasMaxLength(500);
            entity.Property(device => device.ApiKeyHash).HasMaxLength(128).IsRequired();
            entity.Property(device => device.ObservedPublicIpAddress).HasMaxLength(64);
            entity.Property(device => device.Hostname).HasMaxLength(160);
            entity.Property(device => device.AgentVersion).HasMaxLength(80);
            entity.HasMany(device => device.Sensors).WithOne().HasForeignKey(sensor => sensor.DeviceId);
            entity.HasMany(device => device.Actuators).WithOne().HasForeignKey(actuator => actuator.DeviceId);
        });

        modelBuilder.Entity<Sensor>(entity =>
        {
            entity.ToTable("devices_sensors");
            entity.HasKey(sensor => sensor.Id);
            entity.Property(sensor => sensor.DeviceId).HasMaxLength(80).IsRequired();
            entity.Property(sensor => sensor.Name).HasMaxLength(120).IsRequired();
            entity.Property(sensor => sensor.Type).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.HasIndex(sensor => new { sensor.DeviceId, sensor.Name }).IsUnique();
        });

        modelBuilder.Entity<Actuator>(entity =>
        {
            entity.ToTable("devices_actuators");
            entity.HasKey(actuator => actuator.Id);
            entity.Property(actuator => actuator.DeviceId).HasMaxLength(80).IsRequired();
            entity.Property(actuator => actuator.Name).HasMaxLength(120).IsRequired();
            entity.Property(actuator => actuator.Type).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.HasIndex(actuator => new { actuator.DeviceId, actuator.Name }).IsUnique();
        });
    }
}

public sealed class DevicesService(DevicesDbContext dbContext) : IDevicesService
{
    public async Task<Result<DeviceCreatedResponse>> CreateDeviceAsync(CreateDeviceRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Id) || string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<DeviceCreatedResponse>.Failure("devices.invalid", "Device id and name are required.");
        }

        var deviceId = request.Id.Trim();
        if (await dbContext.Devices.AnyAsync(device => device.Id == deviceId, cancellationToken))
        {
            return Result<DeviceCreatedResponse>.Failure("devices.exists", "Device already exists.");
        }

        var apiKey = ApiKeyHasher.GenerateKey();
        var device = new Device
        {
            Id = deviceId,
            Name = request.Name.Trim(),
            Description = request.Description,
            ApiKeyHash = ApiKeyHasher.Hash(apiKey),
            CreatedAtUtc = Clock.UtcNow
        };

        dbContext.Devices.Add(device);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<DeviceCreatedResponse>.Success(new DeviceCreatedResponse(ToResponse(device), apiKey));
    }

    public async Task<IReadOnlyList<DeviceResponse>> GetDevicesAsync(CancellationToken cancellationToken) =>
        await dbContext.Devices.AsNoTracking().OrderBy(device => device.Id).Select(device => ToResponse(device)).ToListAsync(cancellationToken);

    public async Task<Result<DeviceResponse>> GetDeviceAsync(string deviceId, CancellationToken cancellationToken)
    {
        var device = await dbContext.Devices.AsNoTracking().SingleOrDefaultAsync(item => item.Id == deviceId, cancellationToken);
        return device is null
            ? Result<DeviceResponse>.Failure("devices.not_found", "Device not found.")
            : Result<DeviceResponse>.Success(ToResponse(device));
    }

    public async Task<Result<RotateApiKeyResponse>> RotateApiKeyAsync(string deviceId, CancellationToken cancellationToken)
    {
        var device = await dbContext.Devices.SingleOrDefaultAsync(item => item.Id == deviceId, cancellationToken);
        if (device is null)
        {
            return Result<RotateApiKeyResponse>.Failure("devices.not_found", "Device not found.");
        }

        var apiKey = ApiKeyHasher.GenerateKey();
        device.ApiKeyHash = ApiKeyHasher.Hash(apiKey);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<RotateApiKeyResponse>.Success(new RotateApiKeyResponse(device.Id, apiKey));
    }

    public async Task<Result<DeviceHeartbeatResponse>> RegisterHeartbeatAsync(
        string deviceId,
        DeviceHeartbeatRequest request,
        string apiKey,
        string? observedPublicIpAddress,
        CancellationToken cancellationToken)
    {
        var device = await dbContext.Devices.SingleOrDefaultAsync(item => item.Id == deviceId && item.IsActive, cancellationToken);
        if (device is null || !ApiKeyHasher.Verify(apiKey, device.ApiKeyHash))
        {
            return Result<DeviceHeartbeatResponse>.Failure("devices.unauthorized_device", "Invalid device id or API key.");
        }

        var now = Clock.UtcNow;
        device.LastSeenAtUtc = now;
        device.ObservedPublicIpAddress = TrimToNull(observedPublicIpAddress, 64);
        device.Hostname = TrimToNull(request.Hostname, 160);
        device.AgentVersion = TrimToNull(request.AgentVersion, 80);

        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<DeviceHeartbeatResponse>.Success(new DeviceHeartbeatResponse(
            device.Id,
            now,
            device.ObservedPublicIpAddress,
            device.Hostname,
            device.AgentVersion));
    }

    public async Task<Result<SensorResponse>> CreateSensorAsync(string deviceId, CreateSensorRequest request, CancellationToken cancellationToken)
    {
        if (!await dbContext.Devices.AnyAsync(device => device.Id == deviceId, cancellationToken))
        {
            return Result<SensorResponse>.Failure("devices.not_found", "Device not found.");
        }

        if (!Enum.TryParse<SensorType>(request.Type, true, out var type) || string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<SensorResponse>.Failure("devices.invalid_sensor", "Sensor name and type Flow or Level are required.");
        }

        var sensor = new Sensor { DeviceId = deviceId, Name = request.Name.Trim(), Type = type, GpioPin = request.GpioPin, CreatedAtUtc = Clock.UtcNow };
        dbContext.Sensors.Add(sensor);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<SensorResponse>.Success(ToResponse(sensor));
    }

    public async Task<IReadOnlyList<SensorResponse>> GetSensorsAsync(string deviceId, CancellationToken cancellationToken) =>
        await dbContext.Sensors.AsNoTracking().Where(sensor => sensor.DeviceId == deviceId).Select(sensor => ToResponse(sensor)).ToListAsync(cancellationToken);

    public async Task<Result<ActuatorResponse>> CreateActuatorAsync(string deviceId, CreateActuatorRequest request, CancellationToken cancellationToken)
    {
        if (!await dbContext.Devices.AnyAsync(device => device.Id == deviceId && device.IsActive, cancellationToken))
        {
            return Result<ActuatorResponse>.Failure("devices.not_found", "Active device not found.");
        }

        if (!Enum.TryParse<ActuatorType>(request.Type, true, out var type) || string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<ActuatorResponse>.Failure("devices.invalid_actuator", "Actuator name and type Pump or Valve are required.");
        }

        var actuator = new Actuator { DeviceId = deviceId, Name = request.Name.Trim(), Type = type, GpioPin = request.GpioPin, ActiveLow = request.ActiveLow, CreatedAtUtc = Clock.UtcNow };
        dbContext.Actuators.Add(actuator);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<ActuatorResponse>.Success(ToResponse(actuator));
    }

    public async Task<IReadOnlyList<ActuatorResponse>> GetActuatorsAsync(string deviceId, CancellationToken cancellationToken) =>
        await dbContext.Actuators.AsNoTracking().Where(actuator => actuator.DeviceId == deviceId).Select(actuator => ToResponse(actuator)).ToListAsync(cancellationToken);

    public async Task<bool> ValidateApiKeyAsync(string deviceId, string apiKey, CancellationToken cancellationToken)
    {
        var device = await dbContext.Devices.AsNoTracking().SingleOrDefaultAsync(item => item.Id == deviceId && item.IsActive, cancellationToken);
        return device is not null && ApiKeyHasher.Verify(apiKey, device.ApiKeyHash);
    }

    private static DeviceResponse ToResponse(Device device) =>
        new(
            device.Id,
            device.Name,
            device.Description,
            device.IsActive,
            device.CreatedAtUtc,
            device.LastSeenAtUtc,
            device.ObservedPublicIpAddress,
            device.Hostname,
            device.AgentVersion);

    private static SensorResponse ToResponse(Sensor sensor) =>
        new(sensor.Id, sensor.DeviceId, sensor.Name, sensor.Type.ToString(), sensor.GpioPin, sensor.IsActive, sensor.CreatedAtUtc);

    private static ActuatorResponse ToResponse(Actuator actuator) =>
        new(actuator.Id, actuator.DeviceId, actuator.Name, actuator.Type.ToString(), actuator.GpioPin, actuator.ActiveLow, actuator.IsActive, actuator.CreatedAtUtc);

    private static string? TrimToNull(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}

public static class DevicesInfrastructureExtensions
{
    public static IServiceCollection AddDevicesInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<DevicesDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");
            options.UseMySQL(connectionString);
        });

        services.AddScoped<IDevicesService, DevicesService>();
        return services;
    }
}

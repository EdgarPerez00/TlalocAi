using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using TlalocAi.SharedKernel;
using TlalocAi.Telemetry.Application;
using TlalocAi.Telemetry.Domain;

namespace TlalocAi.Telemetry.Infrastructure;

public sealed class TelemetryDbContext(DbContextOptions<TelemetryDbContext> options) : DbContext(options)
{
    public DbSet<Measurement> Measurements => Set<Measurement>();
    public DbSet<LevelMeasurement> LevelMeasurements => Set<LevelMeasurement>();
    public DbSet<ActuatorSnapshot> ActuatorSnapshots => Set<ActuatorSnapshot>();
    public DbSet<Experiment> Experiments => Set<Experiment>();
    public DbSet<DeviceRecord> Devices => Set<DeviceRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Measurement>(entity =>
        {
            entity.ToTable("telemetry_measurements");
            entity.HasKey(measurement => measurement.Id);
            entity.Property(measurement => measurement.DeviceId).HasMaxLength(80).IsRequired();
            entity.Property(measurement => measurement.FlowLpm).HasPrecision(12, 4);
            entity.Property(measurement => measurement.TotalLiters).HasPrecision(14, 4);
            entity.Property(measurement => measurement.DetailedStateJson).HasColumnType("json");
            entity.HasMany(measurement => measurement.Levels).WithOne().HasForeignKey(level => level.MeasurementId);
            entity.HasMany(measurement => measurement.Actuators).WithOne().HasForeignKey(actuator => actuator.MeasurementId);
            entity.HasIndex(measurement => new { measurement.DeviceId, measurement.TimestampUtc });
        });

        modelBuilder.Entity<LevelMeasurement>(entity =>
        {
            entity.ToTable("telemetry_level_measurements");
            entity.HasKey(level => level.Id);
            entity.Property(level => level.SensorName).HasMaxLength(120).IsRequired();
        });

        modelBuilder.Entity<ActuatorSnapshot>(entity =>
        {
            entity.ToTable("telemetry_actuator_snapshots");
            entity.HasKey(snapshot => snapshot.Id);
            entity.Property(snapshot => snapshot.ActuatorName).HasMaxLength(120).IsRequired();
        });

        modelBuilder.Entity<Experiment>(entity =>
        {
            entity.ToTable("telemetry_experiments");
            entity.HasKey(experiment => experiment.Id);
            entity.Property(experiment => experiment.DeviceId).HasMaxLength(80).IsRequired();
            entity.Property(experiment => experiment.Name).HasMaxLength(160).IsRequired();
            entity.Property(experiment => experiment.Description).HasMaxLength(500);
            entity.Property(experiment => experiment.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
        });

        modelBuilder.Entity<DeviceRecord>(entity =>
        {
            entity.ToTable("devices_devices", table => table.ExcludeFromMigrations());
            entity.HasKey(device => device.Id);
            entity.Property(device => device.Id).HasMaxLength(80);
            entity.Property(device => device.ApiKeyHash).HasMaxLength(128).IsRequired();
            entity.Property(device => device.ObservedPublicIpAddress).HasMaxLength(64);
            entity.Property(device => device.Hostname).HasMaxLength(160);
            entity.Property(device => device.AgentVersion).HasMaxLength(80);
        });
    }
}

public sealed class DeviceRecord
{
    public string Id { get; set; } = string.Empty;
    public string ApiKeyHash { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime? LastSeenAtUtc { get; set; }
    public string? ObservedPublicIpAddress { get; set; }
    public string? Hostname { get; set; }
    public string? AgentVersion { get; set; }
}

public sealed class TelemetryService(TelemetryDbContext dbContext) : ITelemetryService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<TelemetryBatchResponse>> StoreBatchAsync(TelemetryBatchRequest request, string apiKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceId))
        {
            return Result<TelemetryBatchResponse>.Failure("telemetry.device_required", "DeviceId is required.");
        }

        if (request.Measurements.Count == 0)
        {
            return Result<TelemetryBatchResponse>.Failure("telemetry.empty_batch", "Telemetry batch cannot be empty.");
        }

        if (request.Measurements.Count > 500)
        {
            return Result<TelemetryBatchResponse>.Failure("telemetry.batch_too_large", "Telemetry batch cannot contain more than 500 measurements.");
        }

        var device = await dbContext.Devices.SingleOrDefaultAsync(item => item.Id == request.DeviceId && item.IsActive, cancellationToken);
        if (device is null || !ApiKeyHasher.Verify(apiKey, device.ApiKeyHash))
        {
            return Result<TelemetryBatchResponse>.Failure("telemetry.unauthorized_device", "Invalid device id or API key.");
        }

        foreach (var measurement in request.Measurements)
        {
            if (measurement.TimestampUtc == default || measurement.FlowLpm < 0 || measurement.TotalLiters < 0)
            {
                return Result<TelemetryBatchResponse>.Failure("telemetry.invalid_measurement", "Timestamp, FlowLpm and TotalLiters must be valid non-negative values.");
            }
        }

        var now = Clock.UtcNow;
        var entities = request.Measurements.Select(item => new Measurement
        {
            DeviceId = request.DeviceId,
            ExperimentId = item.ExperimentId,
            TimestampUtc = item.TimestampUtc.ToUniversalTime(),
            FlowLpm = item.FlowLpm,
            TotalLiters = item.TotalLiters,
            PumpOn = item.PumpOn,
            CreatedAtUtc = now,
            Levels = item.Levels.Select(level => new LevelMeasurement { SensorName = level.Name, IsActive = level.IsActive }).ToList(),
            Actuators = item.Actuators.Select(snapshot => new ActuatorSnapshot { ActuatorName = snapshot.Name, IsOn = snapshot.IsOn }).ToList()
        }).ToList();

        device.LastSeenAtUtc = now;
        dbContext.Measurements.AddRange(entities);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<TelemetryBatchResponse>.Success(new TelemetryBatchResponse(true, request.Measurements.Count, entities.Count, "Telemetry stored successfully"));
    }

    public async Task<Result<TelemetryBatchResponse>> StoreDeviceTelemetryAsync(string deviceId, DeviceTelemetryRequest request, string apiKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
        {
            return Result<TelemetryBatchResponse>.Failure("telemetry.device_required", "DeviceId is required.");
        }

        var device = await dbContext.Devices.SingleOrDefaultAsync(item => item.Id == deviceId && item.IsActive, cancellationToken);
        if (device is null || !ApiKeyHasher.Verify(apiKey, device.ApiKeyHash))
        {
            return Result<TelemetryBatchResponse>.Failure("telemetry.unauthorized_device", "Invalid device id or API key.");
        }

        if (request.TimestampUtc == default || request.Flow.LitersPerMinute < 0 || request.Flow.TotalLiters < 0)
        {
            return Result<TelemetryBatchResponse>.Failure("telemetry.invalid_measurement", "Timestamp, flow and total liters must be valid non-negative values.");
        }

        if (!IsReservoirValid(request.Tower) || !IsReservoirValid(request.Cistern))
        {
            return Result<TelemetryBatchResponse>.Failure("telemetry.invalid_reservoir", "Tower and cistern levels must be between 0 and 5 with five sensor readings.");
        }

        var now = Clock.UtcNow;
        var measurement = new Measurement
        {
            DeviceId = deviceId,
            TimestampUtc = request.TimestampUtc.ToUniversalTime(),
            FlowLpm = request.Flow.LitersPerMinute,
            TotalLiters = request.Flow.TotalLiters,
            PumpOn = request.Pumps.Any(pump => pump.IsOn),
            DetailedStateJson = JsonSerializer.Serialize(request, JsonOptions),
            CreatedAtUtc = now,
            Levels = BuildLevelMeasurements(request),
            Actuators = BuildActuatorSnapshots(request)
        };

        device.LastSeenAtUtc = now;
        dbContext.Measurements.Add(measurement);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<TelemetryBatchResponse>.Success(new TelemetryBatchResponse(true, 1, 1, "Device telemetry stored successfully"));
    }

    public async Task<IReadOnlyList<MeasurementResponse>> GetHistoryAsync(string deviceId, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken)
    {
        var query = dbContext.Measurements.AsNoTracking().Include(item => item.Levels).Include(item => item.Actuators).Where(item => item.DeviceId == deviceId);
        if (fromUtc.HasValue)
        {
            query = query.Where(item => item.TimestampUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(item => item.TimestampUtc <= toUtc.Value);
        }

        var items = await query.OrderByDescending(item => item.TimestampUtc).Take(1000).ToListAsync(cancellationToken);
        return items.Select(ToResponse).ToList();
    }

    public async Task<Result<MeasurementResponse>> GetLatestAsync(string deviceId, CancellationToken cancellationToken)
    {
        var item = await dbContext.Measurements.AsNoTracking().Include(x => x.Levels).Include(x => x.Actuators)
            .Where(x => x.DeviceId == deviceId)
            .OrderByDescending(x => x.TimestampUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return item is null
            ? Result<MeasurementResponse>.Failure("telemetry.not_found", "No measurement found.")
            : Result<MeasurementResponse>.Success(ToResponse(item));
    }

    public async Task<Result<DeviceStateResponse>> GetDeviceStateAsync(string deviceId, CancellationToken cancellationToken)
    {
        var device = await dbContext.Devices.AsNoTracking().SingleOrDefaultAsync(item => item.Id == deviceId, cancellationToken);
        var measurement = await dbContext.Measurements.AsNoTracking().Include(x => x.Levels).Include(x => x.Actuators)
            .Where(x => x.DeviceId == deviceId)
            .OrderByDescending(x => x.TimestampUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return measurement is null
            ? Result<DeviceStateResponse>.Failure("telemetry.state_not_found", "No device state found.")
            : Result<DeviceStateResponse>.Success(ToStateResponse(deviceId, measurement, device));
    }

    public async Task<Result<ExperimentResponse>> CreateExperimentAsync(CreateExperimentRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DeviceId) || string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<ExperimentResponse>.Failure("experiments.invalid", "DeviceId and name are required.");
        }

        if (!await dbContext.Devices.AnyAsync(device => device.Id == request.DeviceId && device.IsActive, cancellationToken))
        {
            return Result<ExperimentResponse>.Failure("experiments.device_not_found", "Active device not found.");
        }

        var experiment = new Experiment
        {
            DeviceId = request.DeviceId,
            Name = request.Name.Trim(),
            Description = request.Description,
            StartedAtUtc = (request.StartedAtUtc ?? Clock.UtcNow).ToUniversalTime(),
            CreatedAtUtc = Clock.UtcNow
        };

        dbContext.Experiments.Add(experiment);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<ExperimentResponse>.Success(ToResponse(experiment));
    }

    public async Task<IReadOnlyList<ExperimentResponse>> GetExperimentsAsync(string? deviceId, CancellationToken cancellationToken)
    {
        var query = dbContext.Experiments.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            query = query.Where(item => item.DeviceId == deviceId);
        }

        return await query.OrderByDescending(item => item.StartedAtUtc).Select(item => ToResponse(item)).ToListAsync(cancellationToken);
    }

    public async Task<Result<ExperimentResponse>> GetExperimentAsync(Guid experimentId, CancellationToken cancellationToken)
    {
        var experiment = await dbContext.Experiments.AsNoTracking().SingleOrDefaultAsync(item => item.Id == experimentId, cancellationToken);
        return experiment is null
            ? Result<ExperimentResponse>.Failure("experiments.not_found", "Experiment not found.")
            : Result<ExperimentResponse>.Success(ToResponse(experiment));
    }

    public async Task<Result<ExperimentResponse>> FinishExperimentAsync(Guid experimentId, CancellationToken cancellationToken)
    {
        var experiment = await dbContext.Experiments.SingleOrDefaultAsync(item => item.Id == experimentId, cancellationToken);
        if (experiment is null)
        {
            return Result<ExperimentResponse>.Failure("experiments.not_found", "Experiment not found.");
        }

        experiment.Status = ExperimentStatus.Finished;
        experiment.EndedAtUtc = Clock.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<ExperimentResponse>.Success(ToResponse(experiment));
    }

    private static MeasurementResponse ToResponse(Measurement measurement) =>
        new(
            measurement.Id,
            measurement.DeviceId,
            measurement.ExperimentId,
            measurement.TimestampUtc,
            measurement.FlowLpm,
            measurement.TotalLiters,
            measurement.PumpOn,
            measurement.Levels.Select(level => new LevelResponse(level.SensorName, level.IsActive)).ToList(),
            measurement.Actuators.Select(snapshot => new ActuatorSnapshotResponse(snapshot.ActuatorName, snapshot.IsOn)).ToList(),
            measurement.DetailedStateJson);

    private static ExperimentResponse ToResponse(Experiment experiment) =>
        new(experiment.Id, experiment.DeviceId, experiment.Name, experiment.Description, experiment.StartedAtUtc, experiment.EndedAtUtc, experiment.Status.ToString(), experiment.CreatedAtUtc);

    private static bool IsReservoirValid(ReservoirTelemetryState reservoir) =>
        reservoir.Level is >= 0 and <= 5 && reservoir.Sensors.Count == 5;

    private static List<LevelMeasurement> BuildLevelMeasurements(DeviceTelemetryRequest request)
    {
        var levels = new List<LevelMeasurement>();
        levels.AddRange(request.Tower.Sensors.Select((active, index) => new LevelMeasurement { SensorName = $"tower_level_{index + 1}", IsActive = active }));
        levels.AddRange(request.Cistern.Sensors.Select((active, index) => new LevelMeasurement { SensorName = $"cistern_level_{index + 1}", IsActive = active }));
        levels.AddRange(request.Containers.OrderBy(item => item.ContainerId).Select(container => new LevelMeasurement { SensorName = $"container_{container.ContainerId}_full", IsActive = container.IsFull }));
        levels.Add(new LevelMeasurement { SensorName = "flow_no_flow_alert", IsActive = request.Flow.NoFlowAlert });
        return levels;
    }

    private static List<ActuatorSnapshot> BuildActuatorSnapshots(DeviceTelemetryRequest request)
    {
        var actuators = new List<ActuatorSnapshot>();
        actuators.AddRange(request.Pumps.Select(pump => new ActuatorSnapshot { ActuatorName = $"pump_{pump.PumpId}", IsOn = pump.IsOn }));
        actuators.AddRange(request.Valves.OrderBy(item => item.ValveId).Select(valve => new ActuatorSnapshot { ActuatorName = $"valve_{valve.ValveId}", IsOn = valve.IsOpen }));
        return actuators;
    }

    private static DeviceStateResponse ToStateResponse(string deviceId, Measurement measurement, DeviceRecord? device)
    {
        if (!string.IsNullOrWhiteSpace(measurement.DetailedStateJson))
        {
            var telemetry = JsonSerializer.Deserialize<DeviceTelemetryRequest>(measurement.DetailedStateJson, JsonOptions);
            if (telemetry is not null)
            {
                return new DeviceStateResponse(
                    deviceId,
                    measurement.TimestampUtc,
                    device?.LastSeenAtUtc,
                    device?.ObservedPublicIpAddress,
                    device?.Hostname,
                    device?.AgentVersion,
                    telemetry.Tower,
                    telemetry.Cistern,
                    telemetry.Flow,
                    telemetry.Pumps,
                    telemetry.Valves,
                    telemetry.Containers,
                    telemetry.Faults,
                    telemetry.Warnings,
                    telemetry.RawInputs);
            }
        }

        var towerSensors = SensorsByPrefix(measurement, "tower_level_");
        var cisternSensors = SensorsByPrefix(measurement, "cistern_level_");
        var pumps = measurement.Actuators
            .Where(item => item.ActuatorName.StartsWith("pump_", StringComparison.OrdinalIgnoreCase) || item.ActuatorName.Equals("pump", StringComparison.OrdinalIgnoreCase))
            .Select(item => new PumpTelemetryState(item.ActuatorName.Replace("pump_", string.Empty, StringComparison.OrdinalIgnoreCase), item.IsOn, false, null))
            .ToList();

        return new DeviceStateResponse(
            deviceId,
            measurement.TimestampUtc,
            device?.LastSeenAtUtc,
            device?.ObservedPublicIpAddress,
            device?.Hostname,
            device?.AgentVersion,
            new ReservoirTelemetryState("tower", towerSensors.Count(item => item), towerSensors, towerSensors.Count(item => item) <= 1, towerSensors.Count != 5),
            new ReservoirTelemetryState("cistern", cisternSensors.Count(item => item), cisternSensors, cisternSensors.Count(item => item) <= 1, cisternSensors.Count != 5),
            new FlowTelemetryState(measurement.FlowLpm, measurement.TotalLiters, 0, measurement.Levels.Any(item => item.SensorName == "flow_no_flow_alert" && item.IsActive)),
            pumps.Count > 0 ? pumps : [new PumpTelemetryState("pump", measurement.PumpOn, false, null)],
            measurement.Actuators
                .Where(item => item.ActuatorName.StartsWith("valve_", StringComparison.OrdinalIgnoreCase))
                .Select(item => new ValveTelemetryState(ParseTrailingNumber(item.ActuatorName), item.IsOn, false, null))
                .Where(item => item.ValveId > 0)
                .OrderBy(item => item.ValveId)
                .ToList(),
            measurement.Levels
                .Where(item => item.SensorName.StartsWith("container_", StringComparison.OrdinalIgnoreCase) && item.SensorName.EndsWith("_full", StringComparison.OrdinalIgnoreCase))
                .Select(item => new ContainerTelemetryState(ParseTrailingNumber(item.SensorName.Replace("_full", string.Empty, StringComparison.OrdinalIgnoreCase)), item.IsActive))
                .Where(item => item.ContainerId > 0)
                .OrderBy(item => item.ContainerId)
                .ToList(),
            [],
            []);
    }

    private static IReadOnlyList<bool> SensorsByPrefix(Measurement measurement, string prefix) =>
        measurement.Levels
            .Where(item => item.SensorName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.SensorName)
            .Select(item => item.IsActive)
            .ToList();

    private static int ParseTrailingNumber(string value)
    {
        var digits = new string(value.Reverse().TakeWhile(char.IsDigit).Reverse().ToArray());
        return int.TryParse(digits, out var number) ? number : 0;
    }
}

public static class TelemetryInfrastructureExtensions
{
    public static IServiceCollection AddTelemetryInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<TelemetryDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");
            options.UseMySQL(connectionString);
        });

        services.AddScoped<ITelemetryService, TelemetryService>();
        return services;
    }
}

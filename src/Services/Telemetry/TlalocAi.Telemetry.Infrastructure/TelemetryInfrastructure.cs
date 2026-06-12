using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        });
    }
}

public sealed class DeviceRecord
{
    public string Id { get; set; } = string.Empty;
    public string ApiKeyHash { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime? LastSeenAtUtc { get; set; }
}

public sealed class TelemetryService(TelemetryDbContext dbContext) : ITelemetryService
{
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
            measurement.Actuators.Select(snapshot => new ActuatorSnapshotResponse(snapshot.ActuatorName, snapshot.IsOn)).ToList());

    private static ExperimentResponse ToResponse(Experiment experiment) =>
        new(experiment.Id, experiment.DeviceId, experiment.Name, experiment.Description, experiment.StartedAtUtc, experiment.EndedAtUtc, experiment.Status.ToString(), experiment.CreatedAtUtc);
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

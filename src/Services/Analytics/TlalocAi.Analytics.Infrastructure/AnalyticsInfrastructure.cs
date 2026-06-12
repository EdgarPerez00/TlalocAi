using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TlalocAi.Analytics.Application;

namespace TlalocAi.Analytics.Infrastructure;

public sealed class AnalyticsReadDbContext(DbContextOptions<AnalyticsReadDbContext> options) : DbContext(options)
{
    public DbSet<MeasurementReadModel> Measurements => Set<MeasurementReadModel>();
    public DbSet<LevelReadModel> Levels => Set<LevelReadModel>();
    public DbSet<ActuatorSnapshotReadModel> Actuators => Set<ActuatorSnapshotReadModel>();
    public DbSet<ExperimentReadModel> Experiments => Set<ExperimentReadModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MeasurementReadModel>(entity =>
        {
            entity.ToTable("telemetry_measurements", table => table.ExcludeFromMigrations());
            entity.HasKey(item => item.Id);
            entity.Property(item => item.DeviceId).HasMaxLength(80);
            entity.Property(item => item.FlowLpm).HasPrecision(12, 4);
            entity.Property(item => item.TotalLiters).HasPrecision(14, 4);
        });

        modelBuilder.Entity<LevelReadModel>(entity =>
        {
            entity.ToTable("telemetry_level_measurements", table => table.ExcludeFromMigrations());
            entity.HasKey(item => item.Id);
            entity.Property(item => item.SensorName).HasMaxLength(120);
        });

        modelBuilder.Entity<ActuatorSnapshotReadModel>(entity =>
        {
            entity.ToTable("telemetry_actuator_snapshots", table => table.ExcludeFromMigrations());
            entity.HasKey(item => item.Id);
            entity.Property(item => item.ActuatorName).HasMaxLength(120);
        });

        modelBuilder.Entity<ExperimentReadModel>(entity =>
        {
            entity.ToTable("telemetry_experiments", table => table.ExcludeFromMigrations());
            entity.HasKey(item => item.Id);
            entity.Property(item => item.DeviceId).HasMaxLength(80);
        });
    }
}

public sealed class MeasurementReadModel
{
    public Guid Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public Guid? ExperimentId { get; set; }
    public DateTime TimestampUtc { get; set; }
    public decimal FlowLpm { get; set; }
    public decimal TotalLiters { get; set; }
    public bool PumpOn { get; set; }
}

public sealed class LevelReadModel
{
    public Guid Id { get; set; }
    public Guid MeasurementId { get; set; }
    public string SensorName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class ActuatorSnapshotReadModel
{
    public Guid Id { get; set; }
    public Guid MeasurementId { get; set; }
    public string ActuatorName { get; set; } = string.Empty;
    public bool IsOn { get; set; }
}

public sealed class ExperimentReadModel
{
    public Guid Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? EndedAtUtc { get; set; }
}

public sealed class AnalyticsService(AnalyticsReadDbContext dbContext) : IAnalyticsService
{
    public async Task<AnalyticsSummaryResponse> GetSummaryAsync(string deviceId, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken)
    {
        var from = fromUtc ?? DateTime.UtcNow.Date;
        var to = toUtc ?? DateTime.UtcNow;
        var measurements = await QueryMeasurements(deviceId, from, to).OrderBy(item => item.TimestampUtc).ToListAsync(cancellationToken);
        return await BuildSummaryAsync(deviceId, from, to, measurements, cancellationToken);
    }

    public async Task<IReadOnlyList<FlowSeriesPoint>> GetFlowSeriesAsync(string deviceId, DateTime? fromUtc, DateTime? toUtc, int bucketSeconds, CancellationToken cancellationToken)
    {
        bucketSeconds = Math.Clamp(bucketSeconds, 1, 3600);
        var from = fromUtc ?? DateTime.UtcNow.Date;
        var to = toUtc ?? DateTime.UtcNow;
        var items = await QueryMeasurements(deviceId, from, to).ToListAsync(cancellationToken);
        return items
            .GroupBy(item => new DateTime((item.TimestampUtc.Ticks / TimeSpan.FromSeconds(bucketSeconds).Ticks) * TimeSpan.FromSeconds(bucketSeconds).Ticks, DateTimeKind.Utc))
            .OrderBy(group => group.Key)
            .Select(group => new FlowSeriesPoint(group.Key, group.Average(item => item.FlowLpm), group.Count()))
            .ToList();
    }

    public async Task<IReadOnlyList<LevelSummaryResponse>> GetLevelsSummaryAsync(string deviceId, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken)
    {
        var measurements = await QueryMeasurements(deviceId, fromUtc ?? DateTime.UtcNow.Date, toUtc ?? DateTime.UtcNow).Select(item => item.Id).ToListAsync(cancellationToken);
        return await dbContext.Levels.AsNoTracking()
            .Where(level => measurements.Contains(level.MeasurementId))
            .GroupBy(level => level.SensorName)
            .Select(group => new LevelSummaryResponse(group.Key, group.Count(level => level.IsActive), group.Count(level => !level.IsActive)))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ActuatorSummaryResponse>> GetActuatorsSummaryAsync(string deviceId, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken)
    {
        var from = fromUtc ?? DateTime.UtcNow.Date;
        var to = toUtc ?? DateTime.UtcNow;
        var measurements = await QueryMeasurements(deviceId, from, to).OrderBy(item => item.TimestampUtc).ToListAsync(cancellationToken);
        var snapshots = await dbContext.Actuators.AsNoTracking()
            .Where(snapshot => measurements.Select(item => item.Id).Contains(snapshot.MeasurementId))
            .ToListAsync(cancellationToken);

        return snapshots.GroupBy(snapshot => snapshot.ActuatorName)
            .Select(group => new ActuatorSummaryResponse(
                group.Key,
                group.Count(snapshot => snapshot.IsOn),
                group.Count(snapshot => !snapshot.IsOn),
                EstimateActiveSeconds(measurements, group.Key, snapshots)))
            .OrderBy(item => item.Name)
            .ToList();
    }

    public async Task<AnalyticsSummaryResponse> GetExperimentSummaryAsync(Guid experimentId, CancellationToken cancellationToken)
    {
        var experiment = await dbContext.Experiments.AsNoTracking().SingleOrDefaultAsync(item => item.Id == experimentId, cancellationToken);
        if (experiment is null)
        {
            return new AnalyticsSummaryResponse(string.Empty, DateTime.MinValue, DateTime.MinValue, 0, 0, 0, 0, 0, 0, null, []);
        }

        var to = experiment.EndedAtUtc ?? DateTime.UtcNow;
        var measurements = await dbContext.Measurements.AsNoTracking()
            .Where(item => item.ExperimentId == experimentId)
            .OrderBy(item => item.TimestampUtc)
            .ToListAsync(cancellationToken);

        return await BuildSummaryAsync(experiment.DeviceId, experiment.StartedAtUtc, to, measurements, cancellationToken);
    }

    private IQueryable<MeasurementReadModel> QueryMeasurements(string deviceId, DateTime fromUtc, DateTime toUtc) =>
        dbContext.Measurements.AsNoTracking()
            .Where(item => item.DeviceId == deviceId && item.TimestampUtc >= fromUtc && item.TimestampUtc <= toUtc);

    private async Task<AnalyticsSummaryResponse> BuildSummaryAsync(string deviceId, DateTime from, DateTime to, IReadOnlyList<MeasurementReadModel> measurements, CancellationToken cancellationToken)
    {
        if (measurements.Count == 0)
        {
            return new AnalyticsSummaryResponse(deviceId, from, to, 0, 0, 0, 0, 0, 0, null, []);
        }

        var measurementIds = measurements.Select(item => item.Id).ToList();
        var snapshots = await dbContext.Actuators.AsNoTracking()
            .Where(snapshot => measurementIds.Contains(snapshot.MeasurementId))
            .ToListAsync(cancellationToken);

        var totalLiters = measurements.Max(item => item.TotalLiters) - measurements.Min(item => item.TotalLiters);
        var pumpSeconds = EstimatePumpSeconds(measurements);
        var actuators = snapshots.GroupBy(snapshot => snapshot.ActuatorName)
            .Select(group =>
            {
                var activeSeconds = EstimateActiveSeconds(measurements, group.Key, snapshots);
                var estimatedLiters = totalLiters == 0 || pumpSeconds == 0 ? 0 : decimal.Round(totalLiters * activeSeconds / pumpSeconds, 4);
                return new ActuatorUsageResponse(group.Key, activeSeconds, estimatedLiters);
            })
            .OrderBy(item => item.Name)
            .ToList();

        return new AnalyticsSummaryResponse(
            deviceId,
            from,
            to,
            decimal.Round(totalLiters, 4),
            decimal.Round(measurements.Average(item => item.FlowLpm), 4),
            measurements.Max(item => item.FlowLpm),
            measurements.Min(item => item.FlowLpm),
            pumpSeconds,
            measurements.Count,
            measurements.Max(item => item.TimestampUtc),
            actuators);
    }

    private static int EstimatePumpSeconds(IReadOnlyList<MeasurementReadModel> measurements)
    {
        var total = 0;
        for (var i = 0; i < measurements.Count - 1; i++)
        {
            if (measurements[i].PumpOn)
            {
                total += (int)Math.Max(0, (measurements[i + 1].TimestampUtc - measurements[i].TimestampUtc).TotalSeconds);
            }
        }

        return total;
    }

    private static int EstimateActiveSeconds(IReadOnlyList<MeasurementReadModel> measurements, string actuatorName, IReadOnlyList<ActuatorSnapshotReadModel> snapshots)
    {
        var byMeasurement = snapshots.Where(item => item.ActuatorName == actuatorName).ToDictionary(item => item.MeasurementId, item => item.IsOn);
        var total = 0;
        for (var i = 0; i < measurements.Count - 1; i++)
        {
            if (byMeasurement.TryGetValue(measurements[i].Id, out var isOn) && isOn)
            {
                total += (int)Math.Max(0, (measurements[i + 1].TimestampUtc - measurements[i].TimestampUtc).TotalSeconds);
            }
        }

        return total;
    }
}

public static class AnalyticsInfrastructureExtensions
{
    public static IServiceCollection AddAnalyticsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AnalyticsReadDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");
            options.UseMySQL(connectionString);
        });

        services.AddScoped<IAnalyticsService, AnalyticsService>();
        return services;
    }
}

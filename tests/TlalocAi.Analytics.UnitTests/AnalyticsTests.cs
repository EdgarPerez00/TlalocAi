using Microsoft.EntityFrameworkCore;
using TlalocAi.Analytics.Infrastructure;

namespace TlalocAi.Analytics.UnitTests;

public class AnalyticsTests
{
    [Fact]
    public async Task Summary_Calculates_Total_Liters_Flow_And_Pump_Runtime()
    {
        await using var db = CreateDbContext();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        db.Measurements.AddRange(
            new MeasurementReadModel { Id = first, DeviceId = "raspberry-calle-01", TimestampUtc = new DateTime(2026, 6, 11, 19, 0, 0, DateTimeKind.Utc), FlowLpm = 1, TotalLiters = 10, PumpOn = true },
            new MeasurementReadModel { Id = second, DeviceId = "raspberry-calle-01", TimestampUtc = new DateTime(2026, 6, 11, 19, 1, 0, DateTimeKind.Utc), FlowLpm = 3, TotalLiters = 12, PumpOn = false });
        db.Actuators.AddRange(
            new ActuatorSnapshotReadModel { Id = Guid.NewGuid(), MeasurementId = first, ActuatorName = "valve_1", IsOn = true },
            new ActuatorSnapshotReadModel { Id = Guid.NewGuid(), MeasurementId = second, ActuatorName = "valve_1", IsOn = false });
        await db.SaveChangesAsync();
        var service = new AnalyticsService(db);

        var summary = await service.GetSummaryAsync("raspberry-calle-01", new DateTime(2026, 6, 11, 18, 0, 0, DateTimeKind.Utc), new DateTime(2026, 6, 11, 20, 0, 0, DateTimeKind.Utc), CancellationToken.None);

        Assert.Equal(2, summary.TotalLiters);
        Assert.Equal(2, summary.AverageFlowLpm);
        Assert.Equal(3, summary.MaxFlowLpm);
        Assert.Equal(60, summary.PumpRuntimeSeconds);
        Assert.Equal(60, summary.Actuators.Single().ActiveSeconds);
    }

    [Fact]
    public async Task Experiment_Summary_Uses_Experiment_Measurements()
    {
        await using var db = CreateDbContext();
        var experimentId = Guid.NewGuid();
        db.Experiments.Add(new ExperimentReadModel { Id = experimentId, DeviceId = "raspberry-calle-01", StartedAtUtc = DateTime.UtcNow.AddMinutes(-5) });
        db.Measurements.Add(new MeasurementReadModel { Id = Guid.NewGuid(), DeviceId = "raspberry-calle-01", ExperimentId = experimentId, TimestampUtc = DateTime.UtcNow, FlowLpm = 1.5m, TotalLiters = 5, PumpOn = true });
        await db.SaveChangesAsync();

        var summary = await new AnalyticsService(db).GetExperimentSummaryAsync(experimentId, CancellationToken.None);

        Assert.Equal("raspberry-calle-01", summary.DeviceId);
        Assert.Equal(1, summary.MeasurementsCount);
    }

    private static AnalyticsReadDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AnalyticsReadDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}

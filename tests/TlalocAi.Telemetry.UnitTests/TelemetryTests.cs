using Microsoft.EntityFrameworkCore;
using TlalocAi.SharedKernel;
using TlalocAi.Telemetry.Application;
using TlalocAi.Telemetry.Infrastructure;

namespace TlalocAi.Telemetry.UnitTests;

public class TelemetryTests
{
    [Fact]
    public async Task Rejects_Empty_Batch()
    {
        await using var db = CreateDbContext();
        var service = new TelemetryService(db);

        var result = await service.StoreBatchAsync(new TelemetryBatchRequest("raspberry-calle-01", DateTime.UtcNow, []), "key", CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Rejects_Negative_Flow()
    {
        var (service, apiKey) = await CreateSeededServiceAsync();
        var request = Batch([new TelemetryMeasurementRequest(DateTime.UtcNow, -1, 0, false, [], [])]);

        var result = await service.StoreBatchAsync(request, apiKey, CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Stores_Valid_Batch_With_Children_And_Updates_LastSeen()
    {
        var db = CreateDbContext();
        var apiKey = "tlaloc_test";
        db.Devices.Add(new DeviceRecord { Id = "raspberry-calle-01", IsActive = true, ApiKeyHash = ApiKeyHasher.Hash(apiKey) });
        await db.SaveChangesAsync();
        var service = new TelemetryService(db);

        var result = await service.StoreBatchAsync(Batch([
            new TelemetryMeasurementRequest(DateTime.UtcNow, 1.25m, 3.45m, true, [new LevelRequest("level_1", true)], [new ActuatorSnapshotRequest("pump", true)])
        ]), apiKey, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value!.Stored);
        Assert.Single(db.Measurements);
        Assert.Single(db.LevelMeasurements);
        Assert.Single(db.ActuatorSnapshots);
        Assert.NotNull(db.Devices.Single().LastSeenAtUtc);
    }

    private static TelemetryBatchRequest Batch(IReadOnlyList<TelemetryMeasurementRequest> measurements) =>
        new("raspberry-calle-01", DateTime.UtcNow, measurements);

    private static async Task<(TelemetryService Service, string ApiKey)> CreateSeededServiceAsync()
    {
        var db = CreateDbContext();
        var apiKey = "tlaloc_test";
        db.Devices.Add(new DeviceRecord { Id = "raspberry-calle-01", IsActive = true, ApiKeyHash = ApiKeyHasher.Hash(apiKey) });
        await db.SaveChangesAsync();
        return (new TelemetryService(db), apiKey);
    }

    private static TelemetryDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<TelemetryDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}

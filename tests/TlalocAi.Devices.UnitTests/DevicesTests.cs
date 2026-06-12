using Microsoft.EntityFrameworkCore;
using TlalocAi.Devices.Application;
using TlalocAi.Devices.Infrastructure;

namespace TlalocAi.Devices.UnitTests;

public class DevicesTests
{
    [Fact]
    public async Task Create_Device_Returns_ApiKey_And_Hashes_Stored_Value()
    {
        await using var db = CreateDbContext();
        var service = new DevicesService(db);

        var result = await service.CreateDeviceAsync(new CreateDeviceRequest("raspberry-calle-01", "Raspberry Calle", null), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.StartsWith("tlaloc_", result.Value!.ApiKey);
        Assert.NotEqual(result.Value.ApiKey, db.Devices.Single().ApiKeyHash);
        Assert.True(await service.ValidateApiKeyAsync("raspberry-calle-01", result.Value.ApiKey, CancellationToken.None));
    }

    [Fact]
    public async Task Create_Sensors_And_Actuators_For_Device()
    {
        await using var db = CreateDbContext();
        var service = new DevicesService(db);
        await service.CreateDeviceAsync(new CreateDeviceRequest("raspberry-calle-01", "Raspberry Calle", null), CancellationToken.None);

        var sensor = await service.CreateSensorAsync("raspberry-calle-01", new CreateSensorRequest("level_1", "Level", 17), CancellationToken.None);
        var actuator = await service.CreateActuatorAsync("raspberry-calle-01", new CreateActuatorRequest("pump", "Pump", 27, false), CancellationToken.None);

        Assert.True(sensor.IsSuccess);
        Assert.True(actuator.IsSuccess);
        Assert.Single(await service.GetSensorsAsync("raspberry-calle-01", CancellationToken.None));
        Assert.Single(await service.GetActuatorsAsync("raspberry-calle-01", CancellationToken.None));
    }

    private static DevicesDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<DevicesDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}

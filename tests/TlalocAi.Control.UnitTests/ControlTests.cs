using Microsoft.EntityFrameworkCore;
using TlalocAi.Control.Application;
using TlalocAi.Control.Infrastructure;
using TlalocAi.SharedKernel;

namespace TlalocAi.Control.UnitTests;

public class ControlTests
{
    [Fact]
    public async Task Creates_Valid_Command()
    {
        var (service, _) = await CreateSeededServiceAsync();

        var result = await service.CreateCommandAsync(new CreateCommandRequest("raspberry-calle-01", "SetActuatorState", "pump", true), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Pending", result.Value!.Status);
    }

    [Fact]
    public async Task Rejects_Command_To_Missing_Actuator()
    {
        var (service, _) = await CreateSeededServiceAsync();

        var result = await service.CreateCommandAsync(new CreateCommandRequest("raspberry-calle-01", "SetActuatorState", "valve_4", true), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Pending_Query_Marks_Command_As_Sent_And_Ack_Executed()
    {
        var (service, apiKey) = await CreateSeededServiceAsync();
        var created = await service.CreateCommandAsync(new CreateCommandRequest("raspberry-calle-01", "SetActuatorState", "pump", true), CancellationToken.None);

        var pending = await service.GetPendingCommandsAsync("raspberry-calle-01", apiKey, CancellationToken.None);
        var ack = await service.AckCommandAsync(created.Value!.Id, new AckCommandRequest("raspberry-calle-01", true, "Command executed", DateTime.UtcNow), apiKey, CancellationToken.None);

        Assert.True(pending.IsSuccess);
        Assert.Single(pending.Value!);
        Assert.True(ack.IsSuccess);
        Assert.Equal("Executed", ack.Value!.Status);
    }

    [Fact]
    public async Task Ack_Failed_Sets_Failed_Status()
    {
        var (service, apiKey) = await CreateSeededServiceAsync();
        var created = await service.CreateCommandAsync(new CreateCommandRequest("raspberry-calle-01", "SetActuatorState", "pump", false), CancellationToken.None);

        var ack = await service.AckCommandAsync(created.Value!.Id, new AckCommandRequest("raspberry-calle-01", false, "GPIO error", DateTime.UtcNow), apiKey, CancellationToken.None);

        Assert.True(ack.IsSuccess);
        Assert.Equal("Failed", ack.Value!.Status);
        Assert.Equal("GPIO error", ack.Value.ErrorMessage);
    }

    private static async Task<(ControlService Service, string ApiKey)> CreateSeededServiceAsync()
    {
        var db = new ControlDbContext(new DbContextOptionsBuilder<ControlDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var apiKey = "tlaloc_test";
        db.Devices.Add(new DeviceRecord { Id = "raspberry-calle-01", IsActive = true, ApiKeyHash = ApiKeyHasher.Hash(apiKey) });
        db.Actuators.Add(new ActuatorRecord { Id = Guid.NewGuid(), DeviceId = "raspberry-calle-01", Name = "pump", IsActive = true });
        await db.SaveChangesAsync();
        return (new ControlService(db), apiKey);
    }
}

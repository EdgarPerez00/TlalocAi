using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TlalocAi.Devices.Infrastructure;

public sealed class DevicesDesignTimeDbContextFactory : IDesignTimeDbContextFactory<DevicesDbContext>
{
    private const string DefaultConnection = "server=localhost;port=3306;database=tlalocai_databse;user=tlalocai;password=TlalocaiApp123!";

    public DevicesDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<DevicesDbContext>()
            .UseMySQL(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ?? DefaultConnection)
            .Options;

        return new DevicesDbContext(options);
    }
}

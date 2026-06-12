using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TlalocAi.Devices.Infrastructure;

public sealed class DevicesDesignTimeDbContextFactory : IDesignTimeDbContextFactory<DevicesDbContext>
{
    public DevicesDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<DevicesDbContext>()
            .UseMySQL("server=localhost;port=3306;database=tlalocai_platform;user=tlalocai;password=tlalocai_dev_password")
            .Options;

        return new DevicesDbContext(options);
    }
}

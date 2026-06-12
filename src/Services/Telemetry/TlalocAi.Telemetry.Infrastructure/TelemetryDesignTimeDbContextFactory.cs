using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TlalocAi.Telemetry.Infrastructure;

public sealed class TelemetryDesignTimeDbContextFactory : IDesignTimeDbContextFactory<TelemetryDbContext>
{
    public TelemetryDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TelemetryDbContext>()
            .UseMySQL("server=localhost;port=3306;database=tlalocai_platform;user=tlalocai;password=tlalocai_dev_password")
            .Options;

        return new TelemetryDbContext(options);
    }
}

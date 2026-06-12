using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TlalocAi.Telemetry.Infrastructure;

public sealed class TelemetryDesignTimeDbContextFactory : IDesignTimeDbContextFactory<TelemetryDbContext>
{
    private const string DefaultConnection = "server=localhost;port=3306;database=tlalocai_databse;user=tlalocai;password=TlalocaiApp123!";

    public TelemetryDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TelemetryDbContext>()
            .UseMySQL(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ?? DefaultConnection)
            .Options;

        return new TelemetryDbContext(options);
    }
}

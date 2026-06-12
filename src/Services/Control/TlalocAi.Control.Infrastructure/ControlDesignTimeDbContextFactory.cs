using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TlalocAi.Control.Infrastructure;

public sealed class ControlDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ControlDbContext>
{
    private const string DefaultConnection = "server=localhost;port=3306;database=tlalocai_databse;user=tlalocai;password=TlalocaiApp123!";

    public ControlDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ControlDbContext>()
            .UseMySQL(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ?? DefaultConnection)
            .Options;

        return new ControlDbContext(options);
    }
}

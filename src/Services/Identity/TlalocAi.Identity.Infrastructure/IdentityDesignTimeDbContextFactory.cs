using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TlalocAi.Identity.Infrastructure;

public sealed class IdentityDesignTimeDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    private const string DefaultConnection = "server=localhost;port=3306;database=tlalocai_databse;user=tlalocai;password=TlalocaiApp123!";

    public IdentityDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseMySQL(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ?? DefaultConnection)
            .Options;

        return new IdentityDbContext(options);
    }
}

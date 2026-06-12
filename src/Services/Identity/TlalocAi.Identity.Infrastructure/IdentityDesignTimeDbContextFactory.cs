using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TlalocAi.Identity.Infrastructure;

public sealed class IdentityDesignTimeDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseMySQL("server=localhost;port=3306;database=tlalocai_platform;user=tlalocai;password=tlalocai_dev_password")
            .Options;

        return new IdentityDbContext(options);
    }
}

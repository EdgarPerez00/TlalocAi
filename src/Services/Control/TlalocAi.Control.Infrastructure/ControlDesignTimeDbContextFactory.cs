using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TlalocAi.Control.Infrastructure;

public sealed class ControlDesignTimeDbContextFactory : IDesignTimeDbContextFactory<ControlDbContext>
{
    public ControlDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ControlDbContext>()
            .UseMySQL("server=localhost;port=3306;database=tlalocai_platform;user=tlalocai;password=tlalocai_dev_password")
            .Options;

        return new ControlDbContext(options);
    }
}

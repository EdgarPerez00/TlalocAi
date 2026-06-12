using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TlalocAi.Identity.Application;
using TlalocAi.Identity.Infrastructure;

namespace TlalocAi.Identity.UnitTests;

public class IdentityTests
{
    [Fact]
    public async Task Register_User_Valid_Creates_User()
    {
        var service = CreateService();

        var result = await service.RegisterAsync(new RegisterUserRequest("Admin User", "admin@test.com", "Password123!", "Admin"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("admin@test.com", result.Value!.User.Email);
        Assert.False(string.IsNullOrWhiteSpace(result.Value.AccessToken));
    }

    [Fact]
    public async Task Login_Valid_Returns_Token()
    {
        var service = CreateService();
        await service.RegisterAsync(new RegisterUserRequest("Viewer User", "viewer@test.com", "Password123!", "Viewer"), CancellationToken.None);

        var result = await service.LoginAsync(new LoginRequest("viewer@test.com", "Password123!"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(result.Value!.AccessToken));
    }

    [Fact]
    public async Task Login_Invalid_Fails()
    {
        var service = CreateService();
        await service.RegisterAsync(new RegisterUserRequest("Viewer User", "viewer@test.com", "Password123!", "Viewer"), CancellationToken.None);

        var result = await service.LoginAsync(new LoginRequest("viewer@test.com", "wrong-password"), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    private static IdentityService CreateService()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "TlalocAi",
                ["Jwt:Audience"] = "TlalocAi.Frontend",
                ["Jwt:SigningKey"] = "development-signing-key-change-this-value-please"
            })
            .Build();

        return new IdentityService(new IdentityDbContext(options), configuration);
    }
}

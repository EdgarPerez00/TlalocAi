using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using TlalocAi.Identity.Application;
using TlalocAi.Identity.Domain;
using TlalocAi.SharedKernel;

namespace TlalocAi.Identity.Infrastructure;

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("identity_users");
            entity.HasKey(user => user.Id);
            entity.Property(user => user.FullName).HasMaxLength(160).IsRequired();
            entity.Property(user => user.Email).HasMaxLength(256).IsRequired();
            entity.HasIndex(user => user.Email).IsUnique();
            entity.Property(user => user.PasswordHash).HasMaxLength(512).IsRequired();
            entity.Property(user => user.Role).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(user => user.CreatedAtUtc).IsRequired();
        });
    }
}

public sealed class IdentityService(
    IdentityDbContext dbContext,
    IConfiguration configuration) : IIdentityService
{
    private readonly PasswordHasher<User> _passwordHasher = new();

    public async Task<Result<AuthResponse>> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.FullName) || string.IsNullOrWhiteSpace(request.Email) || request.Password.Length < 8)
        {
            return Result<AuthResponse>.Failure("identity.invalid_register", "FullName, valid email and password with at least 8 characters are required.");
        }

        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
        {
            return Result<AuthResponse>.Failure("identity.invalid_role", "Role must be Admin or Viewer.");
        }

        var email = request.Email.Trim().ToLowerInvariant();
        if (await dbContext.Users.AnyAsync(user => user.Email == email, cancellationToken))
        {
            return Result<AuthResponse>.Failure("identity.email_exists", "A user with this email already exists.");
        }

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = string.Empty,
            Role = role,
            CreatedAtUtc = Clock.UtcNow
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);

        return Result<AuthResponse>.Success(CreateAuthResponse(user));
    }

    public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users.SingleOrDefaultAsync(item => item.Email == email, cancellationToken);
        if (user is null)
        {
            return Result<AuthResponse>.Failure("identity.invalid_credentials", "Invalid email or password.");
        }

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            return Result<AuthResponse>.Failure("identity.invalid_credentials", "Invalid email or password.");
        }

        return Result<AuthResponse>.Success(CreateAuthResponse(user));
    }

    public async Task<Result<UserResponse>> GetMeAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FindAsync([userId], cancellationToken);
        return user is null
            ? Result<UserResponse>.Failure("identity.not_found", "User not found.")
            : Result<UserResponse>.Success(ToResponse(user));
    }

    private AuthResponse CreateAuthResponse(User user)
    {
        var expiresAtUtc = Clock.UtcNow.AddHours(8);
        var signingKey = configuration["Jwt:SigningKey"] ?? "development-signing-key-change-this-value-please";
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"] ?? "TlalocAi",
            audience: configuration["Jwt:Audience"] ?? "TlalocAi.Frontend",
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Role, user.Role.ToString())
            ],
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return new AuthResponse(new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc, ToResponse(user));
    }

    private static UserResponse ToResponse(User user) =>
        new(user.Id, user.FullName, user.Email, user.Role.ToString(), user.CreatedAtUtc);
}

public static class IdentityInfrastructureExtensions
{
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<IdentityDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");
            options.UseMySQL(connectionString);
        });

        services.AddScoped<IIdentityService, IdentityService>();
        return services;
    }
}

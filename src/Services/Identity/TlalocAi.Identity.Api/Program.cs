using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using TlalocAi.Identity.Application;
using TlalocAi.Identity.Infrastructure;
using TlalocAi.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddTlalocServiceDefaults("TlalocAi.Identity.Api");
builder.Services.AddIdentityInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseTlalocServiceDefaults();
await app.ApplyDatabaseMigrationsAsync<IdentityDbContext>();

var auth = app.MapGroup("/api/auth").WithTags("Auth");

auth.MapPost("/register", async Task<Results<Ok<AuthResponse>, ProblemHttpResult>> (
    RegisterUserRequest request,
    IIdentityService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.RegisterAsync(request, cancellationToken);
    return result.IsSuccess ? TypedResults.Ok(result.Value!) : TypedResults.Problem(result.Error.Message, statusCode: StatusCodes.Status400BadRequest);
});

auth.MapPost("/login", async Task<Results<Ok<AuthResponse>, ProblemHttpResult>> (
    LoginRequest request,
    IIdentityService service,
    CancellationToken cancellationToken) =>
{
    var result = await service.LoginAsync(request, cancellationToken);
    return result.IsSuccess ? TypedResults.Ok(result.Value!) : TypedResults.Problem(result.Error.Message, statusCode: StatusCodes.Status401Unauthorized);
});

auth.MapGet("/me", async Task<Results<Ok<UserResponse>, ProblemHttpResult>> (
    ClaimsPrincipal user,
    IIdentityService service,
    CancellationToken cancellationToken) =>
{
    var userIdValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
    if (!Guid.TryParse(userIdValue, out var userId))
    {
        return TypedResults.Problem("Invalid token subject.", statusCode: StatusCodes.Status401Unauthorized);
    }

    var result = await service.GetMeAsync(userId, cancellationToken);
    return result.IsSuccess ? TypedResults.Ok(result.Value!) : TypedResults.Problem(result.Error.Message, statusCode: StatusCodes.Status404NotFound);
}).RequireAuthorization();

app.Run();

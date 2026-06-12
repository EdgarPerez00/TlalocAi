using TlalocAi.SharedKernel;

namespace TlalocAi.Identity.Application;

public sealed record RegisterUserRequest(string FullName, string Email, string Password, string Role = "Viewer");

public sealed record LoginRequest(string Email, string Password);

public sealed record UserResponse(Guid Id, string FullName, string Email, string Role, DateTime CreatedAtUtc);

public sealed record AuthResponse(string AccessToken, DateTime ExpiresAtUtc, UserResponse User);

public interface IIdentityService
{
    Task<Result<AuthResponse>> RegisterAsync(RegisterUserRequest request, CancellationToken cancellationToken);

    Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<Result<UserResponse>> GetMeAsync(Guid userId, CancellationToken cancellationToken);
}

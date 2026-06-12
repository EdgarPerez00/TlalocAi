namespace TlalocAi.Identity.Domain;

public enum UserRole
{
    Admin = 1,
    Viewer = 2
}

public sealed class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string FullName { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public UserRole Role { get; set; } = UserRole.Viewer;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

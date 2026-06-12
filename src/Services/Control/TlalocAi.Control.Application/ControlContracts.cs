using TlalocAi.SharedKernel;

namespace TlalocAi.Control.Application;

public sealed record CreateCommandRequest(string DeviceId, string Type, string Target, bool State);
public sealed record CommandResponse(Guid Id, string DeviceId, string Type, string Target, bool State, string Status, DateTime CreatedAtUtc, DateTime? SentAtUtc, DateTime? ExecutedAtUtc, string? ErrorMessage);
public sealed record PendingCommandResponse(Guid CommandId, string Type, string Target, bool State, DateTime CreatedAtUtc);
public sealed record AckCommandRequest(string DeviceId, bool Success, string? Message, DateTime ExecutedAtUtc);

public interface IControlService
{
    Task<Result<CommandResponse>> CreateCommandAsync(CreateCommandRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<CommandResponse>> GetCommandsAsync(string? deviceId, CancellationToken cancellationToken);
    Task<Result<CommandResponse>> GetCommandAsync(Guid commandId, CancellationToken cancellationToken);
    Task<Result<CommandResponse>> CancelCommandAsync(Guid commandId, CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<PendingCommandResponse>>> GetPendingCommandsAsync(string deviceId, string apiKey, CancellationToken cancellationToken);
    Task<Result<CommandResponse>> AckCommandAsync(Guid commandId, AckCommandRequest request, string apiKey, CancellationToken cancellationToken);
}

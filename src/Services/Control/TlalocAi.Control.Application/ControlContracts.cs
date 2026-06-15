using TlalocAi.SharedKernel;

namespace TlalocAi.Control.Application;

public sealed record CreateCommandRequest(string DeviceId, string Type, string Target, bool State, string? RequestedBy = null, string? Payload = null);
public sealed record DeviceControlCommandRequest(string DeviceId, string? RequestedBy = null, string? Payload = null);
public sealed record CommandResponse(Guid Id, string DeviceId, string Type, string Target, bool State, string Status, DateTime CreatedAtUtc, DateTime? SentAtUtc, DateTime? ExecutedAtUtc, string? ErrorMessage, string? TargetType = null, string? TargetId = null, string? CommandType = null, string? RequestedBy = null, string? Payload = null, string? ResultMessage = null);
public sealed record PendingCommandResponse(Guid CommandId, string Type, string Target, bool State, DateTime CreatedAtUtc, string? TargetType = null, string? TargetId = null, string? CommandType = null, string? Payload = null);
public sealed record AckCommandRequest(string DeviceId, bool Success, string? Message, DateTime ExecutedAtUtc);
public sealed record DeviceCommandAckRequest(bool Success, string? Message, DateTime ExecutedAtUtc);
public sealed record RejectCommandRequest(string DeviceId, string Reason, DateTime ExecutedAtUtc);
public sealed record DeviceCommandRejectRequest(string Reason, DateTime ExecutedAtUtc);

public interface IControlService
{
    Task<Result<CommandResponse>> CreateCommandAsync(CreateCommandRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<CommandResponse>> GetCommandsAsync(string? deviceId, CancellationToken cancellationToken);
    Task<Result<CommandResponse>> GetCommandAsync(Guid commandId, CancellationToken cancellationToken);
    Task<Result<CommandResponse>> CancelCommandAsync(Guid commandId, CancellationToken cancellationToken);
    Task<Result<IReadOnlyList<PendingCommandResponse>>> GetPendingCommandsAsync(string deviceId, string apiKey, CancellationToken cancellationToken);
    Task<Result<CommandResponse>> AckCommandAsync(Guid commandId, AckCommandRequest request, string apiKey, CancellationToken cancellationToken);
    Task<Result<CommandResponse>> RejectCommandAsync(Guid commandId, RejectCommandRequest request, string apiKey, CancellationToken cancellationToken);
}

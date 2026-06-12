using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TlalocAi.Control.Application;
using TlalocAi.Control.Domain;
using TlalocAi.SharedKernel;

namespace TlalocAi.Control.Infrastructure;

public sealed class ControlDbContext(DbContextOptions<ControlDbContext> options) : DbContext(options)
{
    public DbSet<DeviceCommand> Commands => Set<DeviceCommand>();
    public DbSet<DeviceRecord> Devices => Set<DeviceRecord>();
    public DbSet<ActuatorRecord> Actuators => Set<ActuatorRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DeviceCommand>(entity =>
        {
            entity.ToTable("control_commands");
            entity.HasKey(command => command.Id);
            entity.Property(command => command.DeviceId).HasMaxLength(80).IsRequired();
            entity.Property(command => command.Type).HasConversion<string>().HasMaxLength(60).IsRequired();
            entity.Property(command => command.Target).HasMaxLength(120).IsRequired();
            entity.Property(command => command.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
            entity.Property(command => command.ErrorMessage).HasMaxLength(500);
            entity.HasIndex(command => new { command.DeviceId, command.Status });
        });

        modelBuilder.Entity<DeviceRecord>(entity =>
        {
            entity.ToTable("devices_devices", table => table.ExcludeFromMigrations());
            entity.HasKey(device => device.Id);
            entity.Property(device => device.Id).HasMaxLength(80);
            entity.Property(device => device.ApiKeyHash).HasMaxLength(128).IsRequired();
        });

        modelBuilder.Entity<ActuatorRecord>(entity =>
        {
            entity.ToTable("devices_actuators", table => table.ExcludeFromMigrations());
            entity.HasKey(actuator => actuator.Id);
            entity.Property(actuator => actuator.DeviceId).HasMaxLength(80).IsRequired();
            entity.Property(actuator => actuator.Name).HasMaxLength(120).IsRequired();
        });
    }
}

public sealed class DeviceRecord
{
    public string Id { get; set; } = string.Empty;
    public string ApiKeyHash { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class ActuatorRecord
{
    public Guid Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class ControlService(ControlDbContext dbContext) : IControlService
{
    private static readonly HashSet<string> AllowedTargets = new(StringComparer.OrdinalIgnoreCase)
    {
        "pump", "valve_1", "valve_2", "valve_3", "valve_4"
    };

    public async Task<Result<CommandResponse>> CreateCommandAsync(CreateCommandRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<DeviceCommandType>(request.Type, true, out var type))
        {
            return Result<CommandResponse>.Failure("control.invalid_type", "Only SetActuatorState is supported.");
        }

        if (!AllowedTargets.Contains(request.Target))
        {
            return Result<CommandResponse>.Failure("control.invalid_target", "Target must be pump or valve_1 through valve_4.");
        }

        if (!await dbContext.Devices.AnyAsync(device => device.Id == request.DeviceId && device.IsActive, cancellationToken))
        {
            return Result<CommandResponse>.Failure("control.device_not_found", "Active device not found.");
        }

        if (!await dbContext.Actuators.AnyAsync(actuator => actuator.DeviceId == request.DeviceId && actuator.Name == request.Target && actuator.IsActive, cancellationToken))
        {
            return Result<CommandResponse>.Failure("control.actuator_not_found", "Active actuator target not found for this device.");
        }

        var command = new DeviceCommand
        {
            DeviceId = request.DeviceId,
            Type = type,
            Target = request.Target,
            State = request.State,
            CreatedAtUtc = Clock.UtcNow
        };

        dbContext.Commands.Add(command);
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<CommandResponse>.Success(ToResponse(command));
    }

    public async Task<IReadOnlyList<CommandResponse>> GetCommandsAsync(string? deviceId, CancellationToken cancellationToken)
    {
        var query = dbContext.Commands.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            query = query.Where(command => command.DeviceId == deviceId);
        }

        var commands = await query.OrderByDescending(command => command.CreatedAtUtc).Take(500).ToListAsync(cancellationToken);
        return commands.Select(ToResponse).ToList();
    }

    public async Task<Result<CommandResponse>> GetCommandAsync(Guid commandId, CancellationToken cancellationToken)
    {
        var command = await dbContext.Commands.AsNoTracking().SingleOrDefaultAsync(item => item.Id == commandId, cancellationToken);
        return command is null
            ? Result<CommandResponse>.Failure("control.not_found", "Command not found.")
            : Result<CommandResponse>.Success(ToResponse(command));
    }

    public async Task<Result<CommandResponse>> CancelCommandAsync(Guid commandId, CancellationToken cancellationToken)
    {
        var command = await dbContext.Commands.SingleOrDefaultAsync(item => item.Id == commandId, cancellationToken);
        if (command is null)
        {
            return Result<CommandResponse>.Failure("control.not_found", "Command not found.");
        }

        if (command.Status is DeviceCommandStatus.Executed or DeviceCommandStatus.Failed)
        {
            return Result<CommandResponse>.Failure("control.cannot_cancel", "Executed or failed commands cannot be cancelled.");
        }

        command.Status = DeviceCommandStatus.Cancelled;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<CommandResponse>.Success(ToResponse(command));
    }

    public async Task<Result<IReadOnlyList<PendingCommandResponse>>> GetPendingCommandsAsync(string deviceId, string apiKey, CancellationToken cancellationToken)
    {
        if (!await ValidateDeviceAsync(deviceId, apiKey, cancellationToken))
        {
            return Result<IReadOnlyList<PendingCommandResponse>>.Failure("control.unauthorized_device", "Invalid device id or API key.");
        }

        var commands = await dbContext.Commands
            .Where(command => command.DeviceId == deviceId && command.Status == DeviceCommandStatus.Pending)
            .OrderBy(command => command.CreatedAtUtc)
            .Take(50)
            .ToListAsync(cancellationToken);

        var now = Clock.UtcNow;
        foreach (var command in commands)
        {
            command.Status = DeviceCommandStatus.Sent;
            command.SentAtUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<IReadOnlyList<PendingCommandResponse>>.Success(commands.Select(command =>
            new PendingCommandResponse(command.Id, command.Type.ToString(), command.Target, command.State, command.CreatedAtUtc)).ToList());
    }

    public async Task<Result<CommandResponse>> AckCommandAsync(Guid commandId, AckCommandRequest request, string apiKey, CancellationToken cancellationToken)
    {
        if (!await ValidateDeviceAsync(request.DeviceId, apiKey, cancellationToken))
        {
            return Result<CommandResponse>.Failure("control.unauthorized_device", "Invalid device id or API key.");
        }

        var command = await dbContext.Commands.SingleOrDefaultAsync(item => item.Id == commandId && item.DeviceId == request.DeviceId, cancellationToken);
        if (command is null)
        {
            return Result<CommandResponse>.Failure("control.not_found", "Command not found.");
        }

        command.Status = request.Success ? DeviceCommandStatus.Executed : DeviceCommandStatus.Failed;
        command.ExecutedAtUtc = request.ExecutedAtUtc == default ? Clock.UtcNow : request.ExecutedAtUtc.ToUniversalTime();
        command.ErrorMessage = request.Success ? null : request.Message;
        await dbContext.SaveChangesAsync(cancellationToken);
        return Result<CommandResponse>.Success(ToResponse(command));
    }

    private async Task<bool> ValidateDeviceAsync(string deviceId, string apiKey, CancellationToken cancellationToken)
    {
        var device = await dbContext.Devices.AsNoTracking().SingleOrDefaultAsync(item => item.Id == deviceId && item.IsActive, cancellationToken);
        return device is not null && ApiKeyHasher.Verify(apiKey, device.ApiKeyHash);
    }

    private static CommandResponse ToResponse(DeviceCommand command) =>
        new(command.Id, command.DeviceId, command.Type.ToString(), command.Target, command.State, command.Status.ToString(), command.CreatedAtUtc, command.SentAtUtc, command.ExecutedAtUtc, command.ErrorMessage);
}

public static class ControlInfrastructureExtensions
{
    public static IServiceCollection AddControlInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ControlDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");
            options.UseMySQL(connectionString);
        });

        services.AddScoped<IControlService, ControlService>();
        return services;
    }
}

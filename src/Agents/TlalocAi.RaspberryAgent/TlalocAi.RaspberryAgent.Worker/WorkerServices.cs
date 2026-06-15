using TlalocAi.RaspberryAgent.Application;
using TlalocAi.RaspberryAgent.Domain;

namespace TlalocAi.RaspberryAgent.Worker;

public sealed class AgentRuntimeState
{
    private readonly object _gate = new();
    private SystemSnapshot? _latest;

    public SystemSnapshot? Latest
    {
        get
        {
            lock (_gate)
            {
                return _latest;
            }
        }
    }

    public void Set(SystemSnapshot snapshot)
    {
        lock (_gate)
        {
            _latest = snapshot;
        }
    }
}

public sealed class AgentTelemetryWorker(
    SensorPollingService sensorPollingService,
    PumpControlService pumpControlService,
    ValveCommandService valveCommandService,
    TelemetryPublisherService telemetryPublisherService,
    AgentRuntimeState runtimeState,
    TlalocAgentOptions options,
    ILogger<AgentTelemetryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, options.Backend.TelemetryIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var snapshot = await sensorPollingService.ReadSnapshotAsync(stoppingToken);
                await ApplyLocalSafetyAsync(snapshot, stoppingToken);
                runtimeState.Set(snapshot);
                await telemetryPublisherService.PublishAsync(snapshot, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Telemetry cycle failed. Local safety remains active and telemetry will retry.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task ApplyLocalSafetyAsync(SystemSnapshot snapshot, CancellationToken cancellationToken)
    {
        if (snapshot.Tower.Evaluation.IsCritical)
        {
            await pumpControlService.StopAsync("tower", cancellationToken);
        }

        if (snapshot.Cistern.Evaluation.IsCritical || snapshot.Cistern.Evaluation.Level >= 5)
        {
            await pumpControlService.StopAsync("cistern", cancellationToken);
        }

        if (snapshot.Flow.NoFlowAlert && options.Safety.StopPumpWhenNoFlow)
        {
            await pumpControlService.StopAsync("tower", cancellationToken);
            await pumpControlService.StopAsync("cistern", cancellationToken);
        }

        if (snapshot.Tower.Evaluation.IsCritical && options.Safety.CloseValvesWhenTowerCritical)
        {
            foreach (var valve in snapshot.Valves)
            {
                await valveCommandService.CloseAsync(valve.ValveId, cancellationToken);
            }
        }
    }
}

public sealed class AgentHeartbeatWorker(
    HeartbeatService heartbeatService,
    TlalocAgentOptions options,
    ILogger<AgentHeartbeatWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(1, options.Backend.HeartbeatIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await heartbeatService.SendAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Heartbeat failed. Agent will continue offline.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}

public sealed class AgentCommandWorker(
    CommandPollingService commandPollingService,
    SensorPollingService sensorPollingService,
    AgentRuntimeState runtimeState,
    TlalocAgentOptions options,
    ILogger<AgentCommandWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMilliseconds(Math.Max(250, options.Backend.CommandPollingIntervalMilliseconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var snapshot = runtimeState.Latest ?? await sensorPollingService.ReadSnapshotAsync(stoppingToken);
                await commandPollingService.ExecutePendingAsync(snapshot, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Command polling failed. Agent will keep protecting local hardware.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }
}

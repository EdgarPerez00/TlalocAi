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
    SimulatedPlantScenarioService simulationScenarioService,
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
                if (IsSimulationEnabled())
                {
                    var simulationCycle = simulationScenarioService.Advance();
                    await ApplySimulationCycleAsync(simulationCycle, stoppingToken);
                    logger.LogInformation(
                        "Simulation cycle {CycleNumber} executed for scenario {Scenario}. Events: {Events}",
                        simulationCycle.CycleNumber,
                        simulationCycle.Scenario,
                        simulationCycle.Events.Count == 0 ? "none" : string.Join("; ", simulationCycle.Events));
                }

                var snapshot = await sensorPollingService.ReadSnapshotAsync(stoppingToken);
                await ApplyLocalSafetyAsync(snapshot, stoppingToken);
                runtimeState.Set(snapshot);
                await telemetryPublisherService.PublishAsync(snapshot, stoppingToken);
                logger.LogInformation(
                    "Telemetry sent for device {DeviceId}: tower={TowerLevel}, cistern={CisternLevel}, flow={FlowLitersPerMinute} L/min, total={TotalLiters} L, pumps={PumpStates}, valves={ValveStates}, warnings={Warnings}, faults={Faults}",
                    options.Agent.DeviceId,
                    snapshot.Tower.Evaluation.Level,
                    snapshot.Cistern.Evaluation.Level,
                    snapshot.Flow.LitersPerMinute,
                    snapshot.Flow.TotalLiters,
                    string.Join(",", snapshot.Pumps.Select(pump => $"{pump.PumpId}:{(pump.IsOn ? "on" : "off")}")),
                    string.Join(",", snapshot.Valves.Select(valve => $"{valve.ValveId}:{(valve.IsOpen ? "open" : "closed")}{(valve.IsLocked ? ":locked" : string.Empty)}")),
                    snapshot.Warnings.Count == 0 ? "none" : string.Join("; ", snapshot.Warnings),
                    snapshot.Faults.Count == 0 ? "none" : string.Join("; ", snapshot.Faults));
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Telemetry cycle failed. Local safety remains active and telemetry will retry.");
            }

            await Task.Delay(interval, stoppingToken);
        }
    }

    private async Task ApplySimulationCycleAsync(SimulatedPlantCycle simulationCycle, CancellationToken cancellationToken)
    {
        foreach (var pump in simulationCycle.DesiredPumpStates)
        {
            if (pump.Value)
            {
                await pumpControlService.StartAsync(pump.Key, cancellationToken);
            }
            else
            {
                await pumpControlService.StopAsync(pump.Key, cancellationToken);
            }
        }

        foreach (var valve in simulationCycle.DesiredValveStates)
        {
            var result = valve.Value
                ? await valveCommandService.OpenAsync(valve.Key, cancellationToken)
                : await valveCommandService.CloseAsync(valve.Key, cancellationToken);

            if (!result.Success)
            {
                logger.LogInformation("Simulation valve command rejected for valve {ValveId}: {Message}", valve.Key, result.Message);
            }
        }
    }

    private async Task ApplyLocalSafetyAsync(SystemSnapshot snapshot, CancellationToken cancellationToken)
    {
        if (snapshot.Tower.Evaluation.IsCritical)
        {
            logger.LogWarning("Local safety stopping tower pump. Tower level is critical.");
            await pumpControlService.StopAsync("tower", cancellationToken);
        }

        if (snapshot.Cistern.Evaluation.IsCritical || snapshot.Cistern.Evaluation.Level >= 5)
        {
            logger.LogWarning("Local safety stopping cistern pump. Cistern level is {CisternLevel}.", snapshot.Cistern.Evaluation.Level);
            await pumpControlService.StopAsync("cistern", cancellationToken);
        }

        if (snapshot.Flow.NoFlowAlert && options.Safety.StopPumpWhenNoFlow)
        {
            logger.LogWarning("Local safety stopping pumps. No flow detected while pump is on.");
            await pumpControlService.StopAsync("tower", cancellationToken);
            await pumpControlService.StopAsync("cistern", cancellationToken);
        }

        if (snapshot.Tower.Evaluation.IsCritical && options.Safety.CloseValvesWhenTowerCritical)
        {
            foreach (var valve in snapshot.Valves)
            {
                logger.LogWarning("Local safety closing valve {ValveId}. Tower level is critical.", valve.ValveId);
                await valveCommandService.CloseAsync(valve.ValveId, cancellationToken);
            }
        }
    }

    private bool IsSimulationEnabled() =>
        options.Simulation.Enabled || options.Agent.Mode.Equals("Simulation", StringComparison.OrdinalIgnoreCase);
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

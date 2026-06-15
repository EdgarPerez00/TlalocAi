using System.Collections.Concurrent;
using TlalocAi.RaspberryAgent.Domain;

namespace TlalocAi.RaspberryAgent.Application;

public sealed class TlalocAgentOptions
{
    public AgentOptions Agent { get; set; } = new();
    public BackendOptions Backend { get; set; } = new();
    public GpioOptions Gpio { get; set; } = new();
    public ReservoirHardwareOptions Tower { get; set; } = new();
    public ReservoirHardwareOptions Cistern { get; set; } = new();
    public FlowSensorOptions FlowSensor { get; set; } = new();
    public List<Esp32BoardOptions> Esp32Boards { get; set; } = [];
    public AgentSafetyOptions Safety { get; set; } = new();
}

public sealed class AgentOptions
{
    public string DeviceId { get; set; } = "raspberry-main-001";
    public string SiteId { get; set; } = "escom-demo";
    public string Mode { get; set; } = "Simulation";
    public string Version { get; set; } = "1.0.0";
}

public sealed class BackendOptions
{
    public string BaseUrl { get; set; } = "https://TU_BACKEND_EN_NUBE";
    public string ApiKey { get; set; } = "CHANGE_ME";
    public int HeartbeatIntervalSeconds { get; set; } = 10;
    public int TelemetryIntervalSeconds { get; set; } = 2;
    public int CommandPollingIntervalMilliseconds { get; set; } = 1000;
    public bool UseSignalR { get; set; }
}

public sealed class GpioOptions
{
    public string NumberingScheme { get; set; } = "BCM";
}

public sealed class ReservoirHardwareOptions
{
    public int[] LevelSensorPins { get; set; } = [];
    public int PumpOutputPin { get; set; }
    public int MinLevelToRun { get; set; } = 2;
    public int? MaxLevelToRun { get; set; }
    public int UnlockLevel { get; set; } = 3;
}

public sealed class FlowSensorOptions
{
    public int Pin { get; set; } = 23;
    public decimal PulsesPerLiter { get; set; } = 450;
    public int NoFlowTimeoutSeconds { get; set; } = 5;
}

public sealed class Esp32BoardOptions
{
    public string BoardId { get; set; } = string.Empty;
    public string SerialPort { get; set; } = string.Empty;
    public int BaudRate { get; set; } = 115200;
    public int[] ContainerStatusInputPinsOnRaspberry { get; set; } = [];
    public int[] ControlsValves { get; set; } = [];
    public int[] ControlsContainers { get; set; } = [];
}

public sealed class AgentSafetyOptions
{
    public bool CloseValvesWhenTowerCritical { get; set; } = true;
    public bool StopPumpWhenNoFlow { get; set; } = true;
    public bool RejectUnsafeCommands { get; set; } = true;

    public SafetyOptions ToDomain() => new(CloseValvesWhenTowerCritical, StopPumpWhenNoFlow, RejectUnsafeCommands);
}

public sealed record HeartbeatPayload(string Hostname, string AgentVersion, DateTime SentAtUtc);
public sealed record Esp32CommandResult(bool Success, string Message, Esp32BoardSnapshot? Snapshot = null);

public interface IGpioInputReader
{
    Task<bool> ReadAsync(int pin, CancellationToken cancellationToken);
}

public interface IGpioOutputWriter
{
    Task WriteAsync(int pin, bool isOn, CancellationToken cancellationToken);
    bool GetLastState(int pin);
}

public interface IOutputActuator
{
    string Id { get; }
    Task SetAsync(bool isOn, CancellationToken cancellationToken);
}

public interface IPumpDriver : IOutputActuator;
public interface IValveDriver : IOutputActuator;

public interface IFlowPulseCounter
{
    Task<long> GetPulsesAsync(CancellationToken cancellationToken);
}

public interface IEsp32Client
{
    Task<Esp32BoardSnapshot> GetStatusAsync(string boardId, CancellationToken cancellationToken);
    Task<Esp32CommandResult> SendValveCommandAsync(string boardId, int localValveId, AgentCommandType commandType, CancellationToken cancellationToken);
}

public interface IBackendClient
{
    Task SendHeartbeatAsync(HeartbeatPayload heartbeat, CancellationToken cancellationToken);
    Task PublishTelemetryAsync(SystemSnapshot snapshot, CancellationToken cancellationToken);
    Task<IReadOnlyList<PendingDeviceCommand>> GetPendingCommandsAsync(CancellationToken cancellationToken);
    Task AckCommandAsync(CommandExecutionResult result, CancellationToken cancellationToken);
    Task RejectCommandAsync(CommandExecutionResult result, CancellationToken cancellationToken);
}

public interface ITelemetryQueue
{
    void Enqueue(SystemSnapshot snapshot);
    bool TryDequeue(out SystemSnapshot? snapshot);
    int Count { get; }
}

public interface ISafetyEvaluationService
{
    SafetyDecision EvaluateCommand(PendingDeviceCommand command, SystemSnapshot snapshot);
    IReadOnlyList<string> EvaluateFaults(SystemSnapshot snapshot);
}

public sealed class DefaultSafetyEvaluationService(TlalocAgentOptions options) : ISafetyEvaluationService
{
    private readonly SafetyEvaluationService _inner = new();

    public SafetyDecision EvaluateCommand(PendingDeviceCommand command, SystemSnapshot snapshot) =>
        _inner.EvaluateCommand(command, snapshot, options.Safety.ToDomain());

    public IReadOnlyList<string> EvaluateFaults(SystemSnapshot snapshot) =>
        _inner.EvaluateFaults(snapshot, options.Safety.ToDomain());
}

public sealed class OfflineTelemetryQueueService : ITelemetryQueue
{
    private readonly ConcurrentQueue<SystemSnapshot> _queue = new();

    public int Count => _queue.Count;
    public void Enqueue(SystemSnapshot snapshot) => _queue.Enqueue(snapshot);
    public bool TryDequeue(out SystemSnapshot? snapshot) => _queue.TryDequeue(out snapshot);
}

public sealed class FlowSensorService(IFlowPulseCounter pulseCounter, TlalocAgentOptions options)
{
    private long _previousPulses;
    private DateTime _lastReadUtc = DateTime.UtcNow;
    private DateTime _lastPulseUtc = DateTime.UtcNow;

    public async Task<FlowSnapshot> ReadAsync(bool pumpRunning, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var pulses = await pulseCounter.GetPulsesAsync(cancellationToken);
        if (pulses > _previousPulses)
        {
            _lastPulseUtc = now;
        }

        var snapshot = FlowCalculator.Calculate(
            _previousPulses,
            pulses,
            options.FlowSensor.PulsesPerLiter,
            now - _lastReadUtc,
            pumpRunning,
            TimeSpan.FromSeconds(options.FlowSensor.NoFlowTimeoutSeconds),
            now - _lastPulseUtc);

        _previousPulses = pulses;
        _lastReadUtc = now;
        return snapshot;
    }
}

public sealed class PumpControlService(IGpioOutputWriter outputWriter, TlalocAgentOptions options)
{
    public async Task StartAsync(string pumpId, CancellationToken cancellationToken) =>
        await SetAsync(pumpId, true, cancellationToken);

    public async Task StopAsync(string pumpId, CancellationToken cancellationToken) =>
        await SetAsync(pumpId, false, cancellationToken);

    public bool IsOn(string pumpId) => outputWriter.GetLastState(ResolvePumpPin(pumpId));

    private async Task SetAsync(string pumpId, bool isOn, CancellationToken cancellationToken) =>
        await outputWriter.WriteAsync(ResolvePumpPin(pumpId), isOn, cancellationToken);

    private int ResolvePumpPin(string pumpId)
    {
        var normalized = pumpId.Replace("pump_", string.Empty, StringComparison.OrdinalIgnoreCase);
        return normalized.ToLowerInvariant() switch
        {
            "1" or "tower" or "torre" or "pump" => options.Tower.PumpOutputPin,
            "2" or "cistern" or "cisterna" => options.Cistern.PumpOutputPin,
            _ => throw new InvalidOperationException($"Unknown pump target '{pumpId}'.")
        };
    }
}

public sealed class ValveCommandService(IEsp32Client esp32Client, TlalocAgentOptions options)
{
    public async Task<Esp32CommandResult> OpenAsync(int valveId, CancellationToken cancellationToken) =>
        await SendAsync(valveId, AgentCommandType.Open, cancellationToken);

    public async Task<Esp32CommandResult> CloseAsync(int valveId, CancellationToken cancellationToken) =>
        await SendAsync(valveId, AgentCommandType.Close, cancellationToken);

    private async Task<Esp32CommandResult> SendAsync(int valveId, AgentCommandType commandType, CancellationToken cancellationToken)
    {
        var board = options.Esp32Boards.SingleOrDefault(item => item.ControlsValves.Contains(valveId));
        if (board is null)
        {
            return new Esp32CommandResult(false, $"No ESP32 board controls valve {valveId}.");
        }

        var localValveId = Array.IndexOf(board.ControlsValves, valveId) + 1;
        return await esp32Client.SendValveCommandAsync(board.BoardId, localValveId, commandType, cancellationToken);
    }
}

public sealed class SensorPollingService(
    IGpioInputReader inputReader,
    FlowSensorService flowSensorService,
    PumpControlService pumpControlService,
    IEsp32Client esp32Client,
    TlalocAgentOptions options)
{
    public async Task<SystemSnapshot> ReadSnapshotAsync(CancellationToken cancellationToken)
    {
        var rawInputs = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        var towerSensors = await ReadPinsAsync("tower_level", options.Tower.LevelSensorPins, rawInputs, cancellationToken);
        var cisternSensors = await ReadPinsAsync("cistern_level", options.Cistern.LevelSensorPins, rawInputs, cancellationToken);
        var esp32Snapshots = await ReadEsp32BoardsAsync(rawInputs, cancellationToken);
        var containers = esp32Snapshots.SelectMany(item => item.Containers).OrderBy(item => item.ContainerId).ToList();
        var valves = esp32Snapshots.SelectMany(item => item.Valves).OrderBy(item => item.ValveId).ToList();
        var towerPumpOn = pumpControlService.IsOn("tower");
        var cisternPumpOn = pumpControlService.IsOn("cistern");
        var flow = await flowSensorService.ReadAsync(towerPumpOn || cisternPumpOn, cancellationToken);
        var tower = new ReservoirSnapshot(
            "tower",
            towerSensors,
            ReservoirLevelCalculator.Calculate(towerSensors, new ReservoirOptions(options.Tower.MinLevelToRun, options.Tower.UnlockLevel)));
        var cistern = new ReservoirSnapshot(
            "cistern",
            cisternSensors,
            ReservoirLevelCalculator.Calculate(cisternSensors, new ReservoirOptions(options.Cistern.MinLevelToRun, options.Cistern.UnlockLevel, options.Cistern.MaxLevelToRun)));

        var faults = esp32Snapshots.Where(item => !item.IsOnline).Select(item => item.Error ?? $"{item.BoardId} is offline.").ToList();
        var warnings = new List<string>();
        if (tower.Evaluation.IsCritical)
        {
            warnings.Add("Tower level is critical.");
        }

        if (cistern.Evaluation.IsCritical)
        {
            warnings.Add("Cistern level is critical.");
        }

        if (flow.NoFlowAlert)
        {
            warnings.Add("No flow detected while pump is on.");
        }

        return new SystemSnapshot(
            DateTime.UtcNow,
            tower,
            cistern,
            flow,
            [
                new PumpSnapshot("tower", towerPumpOn, tower.Evaluation.IsCritical, tower.Evaluation.IsCritical ? "Tower level critical or invalid." : null),
                new PumpSnapshot("cistern", cisternPumpOn, cistern.Evaluation.IsCritical || cistern.Evaluation.Level >= 5, cistern.Evaluation.IsCritical ? "Cistern level critical or invalid." : null)
            ],
            valves,
            containers,
            faults,
            warnings,
            rawInputs);
    }

    private async Task<IReadOnlyList<bool>> ReadPinsAsync(string prefix, IReadOnlyList<int> pins, Dictionary<string, bool> rawInputs, CancellationToken cancellationToken)
    {
        var values = new List<bool>(pins.Count);
        for (var index = 0; index < pins.Count; index++)
        {
            var value = await inputReader.ReadAsync(pins[index], cancellationToken);
            rawInputs[$"{prefix}_{index + 1}"] = value;
            values.Add(value);
        }

        return values;
    }

    private async Task<IReadOnlyList<Esp32BoardSnapshot>> ReadEsp32BoardsAsync(Dictionary<string, bool> rawInputs, CancellationToken cancellationToken)
    {
        var snapshots = new List<Esp32BoardSnapshot>();

        foreach (var board in options.Esp32Boards)
        {
            try
            {
                var snapshot = await esp32Client.GetStatusAsync(board.BoardId, cancellationToken);
                snapshots.Add(snapshot);
            }
            catch (Exception ex)
            {
                var containers = await ReadContainerFallbackAsync(board, rawInputs, cancellationToken);
                var valves = board.ControlsValves.Select(valve => new ValveSnapshot(valve, false, true, "ESP32 state unavailable; valve kept safe.")).ToList();
                snapshots.Add(new Esp32BoardSnapshot(board.BoardId, containers, valves, false, ex.Message));
            }
        }

        return snapshots;
    }

    private async Task<IReadOnlyList<ContainerSnapshot>> ReadContainerFallbackAsync(Esp32BoardOptions board, Dictionary<string, bool> rawInputs, CancellationToken cancellationToken)
    {
        var containers = new List<ContainerSnapshot>();

        for (var index = 0; index < board.ControlsContainers.Length && index < board.ContainerStatusInputPinsOnRaspberry.Length; index++)
        {
            var value = await inputReader.ReadAsync(board.ContainerStatusInputPinsOnRaspberry[index], cancellationToken);
            rawInputs[$"{board.BoardId}_container_{board.ControlsContainers[index]}"] = value;
            containers.Add(new ContainerSnapshot(board.ControlsContainers[index], value));
        }

        return containers;
    }
}

public sealed class TelemetryPublisherService(IBackendClient backendClient, ITelemetryQueue queue)
{
    public async Task PublishAsync(SystemSnapshot snapshot, CancellationToken cancellationToken)
    {
        while (queue.TryDequeue(out var queued) && queued is not null)
        {
            await backendClient.PublishTelemetryAsync(queued, cancellationToken);
        }

        try
        {
            await backendClient.PublishTelemetryAsync(snapshot, cancellationToken);
        }
        catch
        {
            queue.Enqueue(snapshot);
            throw;
        }
    }
}

public sealed class HeartbeatService(IBackendClient backendClient, TlalocAgentOptions options)
{
    public async Task SendAsync(CancellationToken cancellationToken)
    {
        var hostName = Environment.MachineName;
        await backendClient.SendHeartbeatAsync(new HeartbeatPayload(hostName, options.Agent.Version, DateTime.UtcNow), cancellationToken);
    }
}

public sealed class CommandPollingService(
    IBackendClient backendClient,
    ISafetyEvaluationService safetyEvaluationService,
    PumpControlService pumpControlService,
    ValveCommandService valveCommandService)
{
    public async Task ExecutePendingAsync(SystemSnapshot snapshot, CancellationToken cancellationToken)
    {
        var commands = await backendClient.GetPendingCommandsAsync(cancellationToken);

        foreach (var command in commands)
        {
            var decision = safetyEvaluationService.EvaluateCommand(command, snapshot);
            if (!decision.IsAllowed)
            {
                await backendClient.RejectCommandAsync(CommandExecutionResult.Rejected(command.CommandId, decision.Reason), cancellationToken);
                continue;
            }

            var result = await ExecuteCommandAsync(command, cancellationToken);
            if (result.Success)
            {
                await backendClient.AckCommandAsync(result, cancellationToken);
            }
            else
            {
                await backendClient.RejectCommandAsync(result, cancellationToken);
            }
        }
    }

    private async Task<CommandExecutionResult> ExecuteCommandAsync(PendingDeviceCommand command, CancellationToken cancellationToken)
    {
        try
        {
            if (command.TargetType == CommandTargetType.Pump)
            {
                if (command.CommandType == AgentCommandType.Start)
                {
                    await pumpControlService.StartAsync(command.TargetId, cancellationToken);
                    return CommandExecutionResult.Acknowledged(command.CommandId, "Pump started.");
                }

                if (command.CommandType == AgentCommandType.Stop)
                {
                    await pumpControlService.StopAsync(command.TargetId, cancellationToken);
                    return CommandExecutionResult.Acknowledged(command.CommandId, "Pump stopped.");
                }
            }

            if (command.TargetType == CommandTargetType.Valve)
            {
                var valveId = int.Parse(command.TargetId.Replace("valve_", string.Empty, StringComparison.OrdinalIgnoreCase));
                var valveResult = command.CommandType == AgentCommandType.Open
                    ? await valveCommandService.OpenAsync(valveId, cancellationToken)
                    : await valveCommandService.CloseAsync(valveId, cancellationToken);

                return valveResult.Success
                    ? CommandExecutionResult.Acknowledged(command.CommandId, valveResult.Message)
                    : CommandExecutionResult.Rejected(command.CommandId, valveResult.Message);
            }

            return CommandExecutionResult.Rejected(command.CommandId, "Unsupported command target.");
        }
        catch (Exception ex)
        {
            return CommandExecutionResult.Rejected(command.CommandId, ex.Message);
        }
    }
}

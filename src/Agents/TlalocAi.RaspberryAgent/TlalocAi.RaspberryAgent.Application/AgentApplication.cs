using System.Collections.Concurrent;
using TlalocAi.RaspberryAgent.Domain;

namespace TlalocAi.RaspberryAgent.Application;

public sealed class TlalocAgentOptions
{
    public AgentOptions Agent { get; set; } = new();
    public BackendOptions Backend { get; set; } = new();
    public SimulationOptions Simulation { get; set; } = new();
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

public sealed class SimulationOptions
{
    public bool Enabled { get; set; }
    public string Scenario { get; set; } = "Demo";
    public int CycleSeconds { get; set; } = 2;
    public int InjectNoFlowEveryCycles { get; set; } = 8;
    public int InjectCriticalLevelEveryCycles { get; set; } = 6;
    public bool EnableNoFlowScenario { get; set; } = true;
    public bool EnableCriticalLevelScenario { get; set; } = true;
    public bool EnableCisternFullScenario { get; set; } = true;
    public bool EnableValveLockScenario { get; set; } = true;
    public bool AutoTogglePumps { get; set; } = true;
    public bool AutoToggleValves { get; set; } = true;
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

public sealed record SimulatedPlantState(
    long CycleNumber,
    string Scenario,
    int TowerLevel,
    int CisternLevel,
    decimal LitersPerMinute,
    long Pulses,
    IReadOnlyDictionary<string, bool> PumpStates,
    IReadOnlyDictionary<int, bool> ValveRequestedStates,
    IReadOnlyDictionary<int, bool> ContainerFullStates,
    IReadOnlyList<string> Events);

public sealed record SimulatedPlantCycle(
    long CycleNumber,
    string Scenario,
    IReadOnlyDictionary<string, bool> DesiredPumpStates,
    IReadOnlyDictionary<int, bool> DesiredValveStates,
    IReadOnlyList<string> Events,
    SimulatedPlantState State);

public sealed class SimulatedPlantScenarioService
{
    private readonly object _gate = new();
    private readonly TlalocAgentOptions _options;
    private readonly Dictionary<string, bool> _pumpStates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["tower"] = false,
        ["cistern"] = false
    };
    private readonly Dictionary<int, bool> _valveRequestedStates = new();
    private readonly Dictionary<int, bool> _containerFullStates = new();
    private readonly Dictionary<int, int[]> _valveContainerMap = new();
    private SimulatedPlantState _current;
    private long _cycleNumber;
    private long _pulses;
    private decimal _lastLitersPerMinute;
    private bool _suppressFlowThisCycle;
    private int _noFlowHoldCyclesRemaining;

    public SimulatedPlantScenarioService(TlalocAgentOptions options)
    {
        _options = options;
        var valveIds = ResolveValveIds(options);
        var containerIds = ResolveContainerIds(options);

        foreach (var valveId in valveIds)
        {
            _valveRequestedStates[valveId] = false;
        }

        foreach (var containerId in containerIds)
        {
            _containerFullStates[containerId] = false;
        }

        BuildValveContainerMap(valveIds, containerIds);
        _current = CreateState(0, 3, 4, 0m, []);
    }

    public SimulatedPlantState Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public SimulatedPlantCycle Advance()
    {
        lock (_gate)
        {
            _cycleNumber++;
            var scenario = string.IsNullOrWhiteSpace(_options.Simulation.Scenario)
                ? "Demo"
                : _options.Simulation.Scenario.Trim();
            var events = new List<string>();
            var desiredPumps = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            var desiredValves = new Dictionary<int, bool>();
            var nominalTowerLevel = 2 + (int)(_cycleNumber % 4);
            var nominalCisternLevel = 2 + (int)((_cycleNumber + 2) % 4);
            var towerLevel = nominalTowerLevel;
            var cisternLevel = nominalCisternLevel;

            ResetContainers();

            if (ShouldInjectCriticalLevel())
            {
                if (_options.Simulation.EnableCisternFullScenario && (_cycleNumber / Math.Max(1, _options.Simulation.InjectCriticalLevelEveryCycles)) % 3 == 0)
                {
                    cisternLevel = 5;
                    events.Add("Simulated cistern full safety scenario.");
                }
                else if ((_cycleNumber / Math.Max(1, _options.Simulation.InjectCriticalLevelEveryCycles)) % 2 == 0)
                {
                    cisternLevel = 1;
                    events.Add("Simulated cistern critical level.");
                }
                else
                {
                    towerLevel = 1;
                    events.Add("Simulated tower critical level.");
                }
            }

            if (_options.Simulation.AutoTogglePumps)
            {
                desiredPumps["tower"] = !ShouldInjectCriticalLevel() && _cycleNumber % 5 is 1 or 2 or 3;
                desiredPumps["cistern"] = cisternLevel is >= 2 and < 5 && _cycleNumber % 6 is 2 or 3 or 4;
            }

            if (_options.Simulation.AutoToggleValves)
            {
                foreach (var valveId in _valveRequestedStates.Keys.Order())
                {
                    desiredValves[valveId] = ((_cycleNumber + valveId) % 4) < 2;
                }
            }

            if (ShouldInjectValveLock() && _valveContainerMap.Count > 0)
            {
                var valveId = _valveContainerMap.Keys.Order().ElementAt((int)(_cycleNumber % _valveContainerMap.Count));
                var containerId = _valveContainerMap[valveId][0];
                _containerFullStates[containerId] = true;
                desiredValves[valveId] = true;
                events.Add($"Simulated valve {valveId} blocked by full container {containerId}.");
            }

            if (ShouldInjectNoFlow())
            {
                _noFlowHoldCyclesRemaining = Math.Max(
                    _noFlowHoldCyclesRemaining,
                    (int)Math.Ceiling(_options.FlowSensor.NoFlowTimeoutSeconds / (double)Math.Max(1, _options.Simulation.CycleSeconds)) + 1);
            }

            if (_noFlowHoldCyclesRemaining > 0)
            {
                desiredPumps["tower"] = true;
                _suppressFlowThisCycle = true;
                events.Add("Simulated no-flow condition while tower pump is on.");
                _noFlowHoldCyclesRemaining--;
            }
            else
            {
                _suppressFlowThisCycle = false;
            }

            var anyPumpOn = desiredPumps.Count > 0
                ? desiredPumps.Values.Any(item => item)
                : _pumpStates.Values.Any(item => item);
            var anyValveOpen = desiredValves.Count > 0
                ? desiredValves.Any(item => item.Value && !IsValveLockedUnsafe(item.Key))
                : _valveRequestedStates.Any(item => item.Value && !IsValveLockedUnsafe(item.Key));

            _lastLitersPerMinute = CalculateFlow(anyPumpOn, anyValveOpen, _suppressFlowThisCycle);
            var cycleSeconds = Math.Max(1, _options.Simulation.CycleSeconds);
            var litersThisCycle = _lastLitersPerMinute * cycleSeconds / 60m;
            if (litersThisCycle > 0)
            {
                _pulses += (long)Math.Round(litersThisCycle * _options.FlowSensor.PulsesPerLiter, MidpointRounding.AwayFromZero);
            }

            _current = CreateState(_cycleNumber, towerLevel, cisternLevel, _lastLitersPerMinute, events);
            return new SimulatedPlantCycle(_cycleNumber, scenario, desiredPumps, desiredValves, events, _current);
        }
    }

    public void SetPumpState(string pumpId, bool isOn)
    {
        lock (_gate)
        {
            _pumpStates[NormalizePumpId(pumpId)] = isOn;
            _current = CreateState(_current.CycleNumber, _current.TowerLevel, _current.CisternLevel, _current.LitersPerMinute, _current.Events);
        }
    }

    public void SetValveRequestedState(int valveId, bool isOpen)
    {
        lock (_gate)
        {
            _valveRequestedStates[valveId] = isOpen && !IsValveLockedUnsafe(valveId);
            _current = CreateState(_current.CycleNumber, _current.TowerLevel, _current.CisternLevel, _current.LitersPerMinute, _current.Events);
        }
    }

    public bool ReadLevelSensor(string reservoirName, int sensorIndex)
    {
        lock (_gate)
        {
            var level = reservoirName.Equals("tower", StringComparison.OrdinalIgnoreCase)
                ? _current.TowerLevel
                : _current.CisternLevel;
            return sensorIndex >= 0 && sensorIndex < level;
        }
    }

    public bool IsContainerFull(int containerId)
    {
        lock (_gate)
        {
            return _containerFullStates.TryGetValue(containerId, out var isFull) && isFull;
        }
    }

    public ValveSnapshot GetValveSnapshot(int valveId)
    {
        lock (_gate)
        {
            var isLocked = IsValveLockedUnsafe(valveId);
            var requestedOpen = _valveRequestedStates.TryGetValue(valveId, out var isOpen) && isOpen;
            return new ValveSnapshot(
                valveId,
                requestedOpen && !isLocked,
                isLocked,
                isLocked ? "Valve locked by simulated container fill." : null);
        }
    }

    public long GetPulses()
    {
        lock (_gate)
        {
            return _pulses;
        }
    }

    public bool IsPumpOn(string pumpId)
    {
        lock (_gate)
        {
            return _pumpStates.TryGetValue(NormalizePumpId(pumpId), out var isOn) && isOn;
        }
    }

    private SimulatedPlantState CreateState(long cycleNumber, int towerLevel, int cisternLevel, decimal litersPerMinute, IReadOnlyList<string> events) =>
        new(
            cycleNumber,
            string.IsNullOrWhiteSpace(_options.Simulation.Scenario) ? "Demo" : _options.Simulation.Scenario.Trim(),
            Math.Clamp(towerLevel, 0, 5),
            Math.Clamp(cisternLevel, 0, 5),
            decimal.Round(litersPerMinute, 4),
            _pulses,
            new Dictionary<string, bool>(_pumpStates, StringComparer.OrdinalIgnoreCase),
            new Dictionary<int, bool>(_valveRequestedStates),
            new Dictionary<int, bool>(_containerFullStates),
            events.ToArray());

    private void ResetContainers()
    {
        foreach (var containerId in _containerFullStates.Keys.ToArray())
        {
            _containerFullStates[containerId] = ((_cycleNumber + containerId) % 13) == 0;
        }
    }

    private decimal CalculateFlow(bool anyPumpOn, bool anyValveOpen, bool suppressFlow)
    {
        if (!anyPumpOn || suppressFlow)
        {
            return 0m;
        }

        var baseFlow = anyValveOpen ? 12m : 6m;
        var wave = (decimal)(Math.Sin(_cycleNumber / 2.0d) + 1.0d) * 2.5m;
        return decimal.Round(baseFlow + wave, 4);
    }

    private bool ShouldInjectNoFlow() =>
        IsScenarioEnabled("NoFlow")
        && _options.Simulation.EnableNoFlowScenario
        && _options.Simulation.InjectNoFlowEveryCycles > 0
        && _cycleNumber % _options.Simulation.InjectNoFlowEveryCycles == 0;

    private bool ShouldInjectCriticalLevel() =>
        IsScenarioEnabled("Critical")
        && _options.Simulation.EnableCriticalLevelScenario
        && _options.Simulation.InjectCriticalLevelEveryCycles > 0
        && _cycleNumber % _options.Simulation.InjectCriticalLevelEveryCycles == 0;

    private bool ShouldInjectValveLock() =>
        IsScenarioEnabled("ValveLock")
        && _options.Simulation.EnableValveLockScenario
        && _cycleNumber % 5 == 0;

    private bool IsScenarioEnabled(string capability)
    {
        var scenario = string.IsNullOrWhiteSpace(_options.Simulation.Scenario)
            ? "Demo"
            : _options.Simulation.Scenario.Trim();

        return scenario.Equals("Demo", StringComparison.OrdinalIgnoreCase)
            || scenario.Equals("Safety", StringComparison.OrdinalIgnoreCase)
            || scenario.Equals(capability, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsValveLockedUnsafe(int valveId) =>
        _valveContainerMap.TryGetValue(valveId, out var containerIds)
        && containerIds.Any(containerId => _containerFullStates.TryGetValue(containerId, out var isFull) && isFull);

    private void BuildValveContainerMap(IReadOnlyList<int> valveIds, IReadOnlyList<int> containerIds)
    {
        if (valveIds.Count == 0)
        {
            return;
        }

        var orderedValves = valveIds.Order().ToArray();
        var orderedContainers = containerIds.Order().ToArray();
        var groupSize = Math.Max(1, (int)Math.Ceiling(orderedContainers.Length / (double)orderedValves.Length));

        for (var index = 0; index < orderedValves.Length; index++)
        {
            _valveContainerMap[orderedValves[index]] = orderedContainers
                .Skip(index * groupSize)
                .Take(groupSize)
                .DefaultIfEmpty(orderedContainers.LastOrDefault(index + 1))
                .ToArray();
        }
    }

    private static string NormalizePumpId(string pumpId)
    {
        var normalized = pumpId.Replace("pump_", string.Empty, StringComparison.OrdinalIgnoreCase);
        return normalized.Equals("1", StringComparison.OrdinalIgnoreCase) || normalized.Equals("torre", StringComparison.OrdinalIgnoreCase)
            ? "tower"
            : normalized.Equals("2", StringComparison.OrdinalIgnoreCase) || normalized.Equals("cisterna", StringComparison.OrdinalIgnoreCase)
                ? "cistern"
                : normalized.ToLowerInvariant();
    }

    private static IReadOnlyList<int> ResolveValveIds(TlalocAgentOptions options)
    {
        var configured = options.Esp32Boards.SelectMany(board => board.ControlsValves).Where(id => id > 0).Distinct().Order().ToArray();
        return configured.Length == 0 ? [1, 2, 3, 4] : configured;
    }

    private static IReadOnlyList<int> ResolveContainerIds(TlalocAgentOptions options)
    {
        var configured = options.Esp32Boards.SelectMany(board => board.ControlsContainers).Where(id => id > 0).Distinct().Order().ToArray();
        if (configured.Length >= 16)
        {
            return configured;
        }

        return configured.Concat(Enumerable.Range(1, 16)).Distinct().Order().ToArray();
    }
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
        if (tower.Evaluation.HasInvalidReading)
        {
            faults.Add("Tower level reading is inconsistent.");
        }

        if (cistern.Evaluation.HasInvalidReading)
        {
            faults.Add("Cistern level reading is inconsistent.");
        }

        if (tower.Evaluation.IsCritical)
        {
            warnings.Add("Tower level is critical.");
        }

        if (cistern.Evaluation.IsCritical)
        {
            warnings.Add("Cistern level is critical.");
        }

        if (!cistern.Evaluation.IsCritical && cistern.Evaluation.Level >= 5)
        {
            warnings.Add("Cistern level is full.");
        }

        if (flow.NoFlowAlert)
        {
            warnings.Add("No flow detected while pump is on.");
            faults.Add("Pump is running without flow.");
        }

        if (valves.Any(valve => valve.IsLocked))
        {
            faults.Add("At least one valve is locked by container fill safety.");
        }

        return new SystemSnapshot(
            DateTime.UtcNow,
            tower,
            cistern,
            flow,
            [
                new PumpSnapshot("tower", towerPumpOn, tower.Evaluation.IsCritical, tower.Evaluation.IsCritical ? "Tower level critical or invalid." : null),
                new PumpSnapshot(
                    "cistern",
                    cisternPumpOn,
                    cistern.Evaluation.IsCritical || cistern.Evaluation.Level >= 5,
                    cistern.Evaluation.IsCritical
                        ? "Cistern level critical or invalid."
                        : cistern.Evaluation.Level >= 5 ? "Cistern level is full." : null)
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

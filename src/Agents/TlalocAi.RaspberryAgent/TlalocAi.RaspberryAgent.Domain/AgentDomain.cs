namespace TlalocAi.RaspberryAgent.Domain;

public enum CommandTargetType
{
    Pump = 1,
    Valve = 2,
    System = 3
}

public enum AgentCommandType
{
    Open = 1,
    Close = 2,
    Start = 3,
    Stop = 4
}

public sealed record PendingDeviceCommand(
    Guid CommandId,
    CommandTargetType TargetType,
    string TargetId,
    AgentCommandType CommandType,
    string? Payload = null);

public sealed record CommandExecutionResult(Guid CommandId, bool Success, string Message, DateTime ExecutedAtUtc)
{
    public static CommandExecutionResult Acknowledged(Guid commandId, string message) =>
        new(commandId, true, message, DateTime.UtcNow);

    public static CommandExecutionResult Rejected(Guid commandId, string message) =>
        new(commandId, false, message, DateTime.UtcNow);
}

public sealed record ReservoirOptions(int MinLevelToRun, int UnlockLevel, int? MaxLevelToRun = null);
public sealed record SafetyOptions(bool CloseValvesWhenTowerCritical, bool StopPumpWhenNoFlow, bool RejectUnsafeCommands);

public sealed record ReservoirLevelEvaluation(int Level, bool IsCritical, bool HasInvalidReading)
{
    public bool CanRunPump(ReservoirOptions options) =>
        !HasInvalidReading && Level >= options.MinLevelToRun && (!options.MaxLevelToRun.HasValue || Level < options.MaxLevelToRun.Value);

    public bool CanUnlockPump(ReservoirOptions options) =>
        !HasInvalidReading && Level >= options.UnlockLevel && (!options.MaxLevelToRun.HasValue || Level < options.MaxLevelToRun.Value);
}

public sealed record ReservoirSnapshot(
    string Name,
    IReadOnlyList<bool> Sensors,
    ReservoirLevelEvaluation Evaluation,
    string? Message = null);

public sealed record FlowSnapshot(long Pulses, decimal LitersPerMinute, decimal TotalLiters, bool NoFlowAlert);
public sealed record PumpSnapshot(string PumpId, bool IsOn, bool IsBlocked, string? BlockReason);
public sealed record ValveSnapshot(int ValveId, bool IsOpen, bool IsLocked, string? LockReason);
public sealed record ContainerSnapshot(int ContainerId, bool IsFull);

public sealed record Esp32BoardSnapshot(
    string BoardId,
    IReadOnlyList<ContainerSnapshot> Containers,
    IReadOnlyList<ValveSnapshot> Valves,
    bool IsOnline,
    string? Error = null);

public sealed record SystemSnapshot(
    DateTime TimestampUtc,
    ReservoirSnapshot Tower,
    ReservoirSnapshot Cistern,
    FlowSnapshot Flow,
    IReadOnlyList<PumpSnapshot> Pumps,
    IReadOnlyList<ValveSnapshot> Valves,
    IReadOnlyList<ContainerSnapshot> Containers,
    IReadOnlyList<string> Faults,
    IReadOnlyList<string> Warnings,
    IReadOnlyDictionary<string, bool> RawInputs);

public sealed record SafetyDecision(bool IsAllowed, string Reason)
{
    public static SafetyDecision Allow(string reason = "Allowed") => new(true, reason);
    public static SafetyDecision Reject(string reason) => new(false, reason);
}

public static class ReservoirLevelCalculator
{
    public static ReservoirLevelEvaluation Calculate(IReadOnlyList<bool> sensors, ReservoirOptions options)
    {
        if (sensors.Count != 5)
        {
            return new ReservoirLevelEvaluation(0, true, true);
        }

        var invalid = HasInvalidSequence(sensors);
        var active = sensors.Count(sensor => sensor);
        var critical = invalid || active <= 1;
        return new ReservoirLevelEvaluation(active, critical, invalid);
    }

    private static bool HasInvalidSequence(IReadOnlyList<bool> sensors)
    {
        var foundInactiveLowerSensor = false;
        foreach (var sensor in sensors)
        {
            if (!sensor)
            {
                foundInactiveLowerSensor = true;
                continue;
            }

            if (foundInactiveLowerSensor)
            {
                return true;
            }
        }

        return false;
    }
}

public static class ContainerSignalProcessor
{
    public static bool EvaluateContainerFull(bool sensorA, bool sensorB) =>
        (sensorA, sensorB) switch
        {
            (false, false) => false,
            (false, true) => false,
            (true, false) => true,
            (true, true) => true
        };
}

public sealed class ValveSafetyLatch(int valveId, int firstContainerId, int secondContainerId)
{
    public int ValveId { get; } = valveId;
    public int FirstContainerId { get; } = firstContainerId;
    public int SecondContainerId { get; } = secondContainerId;
    public bool IsLocked { get; private set; }

    public ValveSnapshot Evaluate(IReadOnlyCollection<ContainerSnapshot> containers, bool requestedOpen)
    {
        var first = containers.SingleOrDefault(item => item.ContainerId == FirstContainerId)?.IsFull ?? true;
        var second = containers.SingleOrDefault(item => item.ContainerId == SecondContainerId)?.IsFull ?? true;
        var anyFull = first || second;
        var bothEmpty = !first && !second;

        if (anyFull)
        {
            IsLocked = true;
        }
        else if (bothEmpty)
        {
            IsLocked = false;
        }

        var canOpen = requestedOpen && !IsLocked && bothEmpty;
        var reason = IsLocked ? "Valve locked until both associated containers are empty." : null;
        return new ValveSnapshot(ValveId, canOpen, IsLocked, reason);
    }
}

public static class FlowCalculator
{
    public static FlowSnapshot Calculate(
        long previousPulses,
        long currentPulses,
        decimal pulsesPerLiter,
        TimeSpan elapsed,
        bool pumpRunning,
        TimeSpan noFlowTimeout,
        TimeSpan elapsedSinceLastPulse)
    {
        if (pulsesPerLiter <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pulsesPerLiter), "Pulses per liter must be greater than zero.");
        }

        if (elapsed <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(elapsed), "Elapsed time must be greater than zero.");
        }

        var pulseDelta = Math.Max(0, currentPulses - previousPulses);
        var litersDelta = pulseDelta / pulsesPerLiter;
        var litersPerMinute = litersDelta / (decimal)elapsed.TotalMinutes;
        var totalLiters = currentPulses / pulsesPerLiter;
        var noFlow = pumpRunning && elapsedSinceLastPulse >= noFlowTimeout && pulseDelta == 0;

        return new FlowSnapshot(currentPulses, decimal.Round(litersPerMinute, 4), decimal.Round(totalLiters, 4), noFlow);
    }
}

public sealed class SafetyEvaluationService
{
    public SafetyDecision EvaluateCommand(PendingDeviceCommand command, SystemSnapshot snapshot, SafetyOptions safety)
    {
        if (command.TargetType == CommandTargetType.Pump && command.CommandType == AgentCommandType.Stop)
        {
            return SafetyDecision.Allow("Stopping pumps is always allowed.");
        }

        if (command.TargetType == CommandTargetType.Valve && command.CommandType == AgentCommandType.Close)
        {
            return SafetyDecision.Allow("Closing valves is always allowed.");
        }

        if (command.TargetType == CommandTargetType.Pump && command.CommandType == AgentCommandType.Start)
        {
            return EvaluatePumpStart(command.TargetId, snapshot);
        }

        if (command.TargetType == CommandTargetType.Valve && command.CommandType == AgentCommandType.Open)
        {
            return EvaluateValveOpen(command.TargetId, snapshot, safety);
        }

        return SafetyDecision.Allow();
    }

    public IReadOnlyList<string> EvaluateFaults(SystemSnapshot snapshot, SafetyOptions safety)
    {
        var faults = new List<string>();

        if (snapshot.Tower.Evaluation.HasInvalidReading)
        {
            faults.Add("Tower level reading is inconsistent.");
        }

        if (snapshot.Cistern.Evaluation.HasInvalidReading)
        {
            faults.Add("Cistern level reading is inconsistent.");
        }

        if (snapshot.Flow.NoFlowAlert && safety.StopPumpWhenNoFlow)
        {
            faults.Add("Pump is running without flow.");
        }

        if (snapshot.Valves.Any(valve => valve.IsLocked))
        {
            faults.Add("At least one valve is locked by container fill safety.");
        }

        return faults;
    }

    private static SafetyDecision EvaluatePumpStart(string targetId, SystemSnapshot snapshot)
    {
        if (IsTowerPump(targetId) && snapshot.Tower.Evaluation.IsCritical)
        {
            return SafetyDecision.Reject("Tower pump cannot start while tower level is critical or invalid.");
        }

        if (IsCisternPump(targetId) && snapshot.Cistern.Evaluation.IsCritical)
        {
            return SafetyDecision.Reject("Cistern pump cannot start while cistern level is critical or invalid.");
        }

        if (IsCisternPump(targetId) && snapshot.Cistern.Evaluation.Level >= 5)
        {
            return SafetyDecision.Reject("Cistern pump cannot start while cistern is full.");
        }

        return SafetyDecision.Allow("Pump start is safe.");
    }

    private static SafetyDecision EvaluateValveOpen(string targetId, SystemSnapshot snapshot, SafetyOptions safety)
    {
        if (safety.CloseValvesWhenTowerCritical && snapshot.Tower.Evaluation.IsCritical)
        {
            return SafetyDecision.Reject("Valve cannot open while tower level is critical.");
        }

        if (!int.TryParse(targetId.Replace("valve_", string.Empty, StringComparison.OrdinalIgnoreCase), out var valveId))
        {
            return SafetyDecision.Reject("Valve target is invalid.");
        }

        var valve = snapshot.Valves.SingleOrDefault(item => item.ValveId == valveId);
        if (valve is null)
        {
            return SafetyDecision.Reject("Valve state is unavailable.");
        }

        return valve.IsLocked
            ? SafetyDecision.Reject(valve.LockReason ?? "Valve is locked by ESP32 safety state.")
            : SafetyDecision.Allow("Valve open is safe.");
    }

    private static bool IsTowerPump(string targetId) =>
        targetId.Equals("tower", StringComparison.OrdinalIgnoreCase)
        || targetId.Equals("1", StringComparison.OrdinalIgnoreCase)
        || targetId.Equals("pump", StringComparison.OrdinalIgnoreCase)
        || targetId.Equals("pump_tower", StringComparison.OrdinalIgnoreCase);

    private static bool IsCisternPump(string targetId) =>
        targetId.Equals("cistern", StringComparison.OrdinalIgnoreCase)
        || targetId.Equals("cisterna", StringComparison.OrdinalIgnoreCase)
        || targetId.Equals("2", StringComparison.OrdinalIgnoreCase)
        || targetId.Equals("pump_cistern", StringComparison.OrdinalIgnoreCase);
}

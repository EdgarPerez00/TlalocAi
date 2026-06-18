using TlalocAi.RaspberryAgent.Application;
using TlalocAi.RaspberryAgent.Domain;
using TlalocAi.RaspberryAgent.Infrastructure;

namespace TlalocAi.RaspberryAgent.UnitTests;

public class RaspberryAgentTests
{
    [Fact]
    public void Calculates_Tower_Level_With_Five_Sensors()
    {
        var result = ReservoirLevelCalculator.Calculate([true, true, true, true, true], new ReservoirOptions(2, 3));

        Assert.Equal(5, result.Level);
        Assert.False(result.IsCritical);
        Assert.False(result.HasInvalidReading);
    }

    [Fact]
    public void Calculates_Cistern_Level_With_Five_Sensors()
    {
        var result = ReservoirLevelCalculator.Calculate([true, true, true, false, false], new ReservoirOptions(2, 3, 5));

        Assert.Equal(3, result.Level);
        Assert.False(result.IsCritical);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Blocks_Tower_Pump_By_Level_Zero_Or_One(int activeSensors)
    {
        var sensors = Enumerable.Range(0, 5).Select(index => index < activeSensors).ToList();
        var snapshot = CreateSnapshot(towerSensors: sensors);
        var service = new SafetyEvaluationService();

        var decision = service.EvaluateCommand(
            new PendingDeviceCommand(Guid.NewGuid(), CommandTargetType.Pump, "tower", AgentCommandType.Start),
            snapshot,
            new SafetyOptions(true, true, true));

        Assert.False(decision.IsAllowed);
    }

    [Fact]
    public void Unlocks_Tower_Pump_When_Level_Recovers()
    {
        var evaluation = ReservoirLevelCalculator.Calculate([true, true, true, false, false], new ReservoirOptions(2, 3));

        Assert.True(evaluation.CanUnlockPump(new ReservoirOptions(2, 3)));
    }

    [Fact]
    public void Stops_Cistern_Pump_When_Empty()
    {
        var snapshot = CreateSnapshot(cisternSensors: [false, false, false, false, false]);
        var service = new SafetyEvaluationService();

        var decision = service.EvaluateCommand(
            new PendingDeviceCommand(Guid.NewGuid(), CommandTargetType.Pump, "cistern", AgentCommandType.Start),
            snapshot,
            new SafetyOptions(true, true, true));

        Assert.False(decision.IsAllowed);
    }

    [Fact]
    public void Stops_Cistern_Pump_When_Full()
    {
        var snapshot = CreateSnapshot(cisternSensors: [true, true, true, true, true]);
        var service = new SafetyEvaluationService();

        var decision = service.EvaluateCommand(
            new PendingDeviceCommand(Guid.NewGuid(), CommandTargetType.Pump, "cistern", AgentCommandType.Start),
            snapshot,
            new SafetyOptions(true, true, true));

        Assert.False(decision.IsAllowed);
        Assert.Contains("full", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public void Processes_Esp32_Truth_Table(bool sensorA, bool sensorB, bool expectedFull)
    {
        Assert.Equal(expectedFull, ContainerSignalProcessor.EvaluateContainerFull(sensorA, sensorB));
    }

    [Fact]
    public void Closes_Valve_By_Full_Container_And_Blocks_Reopen()
    {
        var latch = new ValveSafetyLatch(1, 1, 2);
        var containers = new[] { new ContainerSnapshot(1, true), new ContainerSnapshot(2, false) };

        var state = latch.Evaluate(containers, requestedOpen: true);

        Assert.False(state.IsOpen);
        Assert.True(state.IsLocked);
    }

    [Fact]
    public void Unlocks_Valve_Only_When_Associated_Containers_Are_Empty()
    {
        var latch = new ValveSafetyLatch(1, 1, 2);
        _ = latch.Evaluate([new ContainerSnapshot(1, true), new ContainerSnapshot(2, false)], requestedOpen: true);

        var unlocked = latch.Evaluate([new ContainerSnapshot(1, false), new ContainerSnapshot(2, false)], requestedOpen: true);

        Assert.True(unlocked.IsOpen);
        Assert.False(unlocked.IsLocked);
    }

    [Fact]
    public void Rejects_Unsafe_Valve_Open_When_Tower_Is_Critical()
    {
        var snapshot = CreateSnapshot(towerSensors: [true, false, false, false, false]);
        var service = new SafetyEvaluationService();

        var decision = service.EvaluateCommand(
            new PendingDeviceCommand(Guid.NewGuid(), CommandTargetType.Valve, "1", AgentCommandType.Open),
            snapshot,
            new SafetyOptions(true, true, true));

        Assert.False(decision.IsAllowed);
    }

    [Fact]
    public void Queues_Offline_Telemetry()
    {
        var queue = new OfflineTelemetryQueueService();
        var snapshot = CreateSnapshot();

        queue.Enqueue(snapshot);

        Assert.Equal(1, queue.Count);
        Assert.True(queue.TryDequeue(out var dequeued));
        Assert.Equal(snapshot.TimestampUtc, dequeued!.TimestampUtc);
    }

    [Fact]
    public async Task Sends_Heartbeat_With_Simulated_Backend()
    {
        var backend = new SimulatedBackendClient();
        var service = new HeartbeatService(backend, CreateOptions());

        await service.SendAsync(CancellationToken.None);
    }

    [Fact]
    public async Task Polling_Commands_Acknowledges_Safe_Stop()
    {
        var options = CreateOptions();
        var backend = new SimulatedBackendClient();
        backend.Enqueue(new PendingDeviceCommand(Guid.NewGuid(), CommandTargetType.Pump, "tower", AgentCommandType.Stop));
        var gpio = new SimulatedGpioState();
        var pumpControl = new PumpControlService(new SimulatedGpioOutputWriter(gpio), options);
        var simulation = new SimulatedPlantScenarioService(options);
        var polling = new CommandPollingService(
            backend,
            new DefaultSafetyEvaluationService(options),
            pumpControl,
            new ValveCommandService(new SimulatedEsp32Client(options, simulation), options));

        await polling.ExecutePendingAsync(CreateSnapshot(), CancellationToken.None);

        Assert.Single(backend.CommandResults);
        Assert.True(backend.CommandResults[0].Success);
    }

    [Fact]
    public void Calculates_Flow()
    {
        var flow = FlowCalculator.Calculate(0, 450, 450, TimeSpan.FromMinutes(1), pumpRunning: true, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(1));

        Assert.Equal(1m, flow.TotalLiters);
        Assert.Equal(1m, flow.LitersPerMinute);
        Assert.False(flow.NoFlowAlert);
    }

    [Fact]
    public void Simulated_Scenario_Generates_Critical_Tower_Level()
    {
        var options = CreateOptions();
        options.Simulation = new SimulationOptions
        {
            Enabled = true,
            Scenario = "Critical",
            CycleSeconds = 2,
            InjectCriticalLevelEveryCycles = 2,
            EnableCriticalLevelScenario = true
        };
        var simulation = new SimulatedPlantScenarioService(options);

        simulation.Advance();
        var cycle = simulation.Advance();

        Assert.Equal(1, cycle.State.TowerLevel);
        Assert.Contains(cycle.Events, item => item.Contains("tower critical", StringComparison.OrdinalIgnoreCase));
        Assert.True(simulation.ReadLevelSensor("tower", 0));
        Assert.False(simulation.ReadLevelSensor("tower", 1));
    }

    [Fact]
    public void Simulated_Scenario_Generates_Sixteen_Container_States()
    {
        var simulation = new SimulatedPlantScenarioService(CreateOptions());

        var cycle = simulation.Advance();

        Assert.Equal(16, cycle.State.ContainerFullStates.Count);
        Assert.Contains(16, cycle.State.ContainerFullStates.Keys);
    }

    [Fact]
    public void Simulated_Scenario_Injects_No_Flow_Event()
    {
        var options = CreateOptions();
        options.Simulation = new SimulationOptions
        {
            Enabled = true,
            Scenario = "NoFlow",
            CycleSeconds = 2,
            InjectNoFlowEveryCycles = 1,
            EnableNoFlowScenario = true,
            AutoTogglePumps = true
        };
        var simulation = new SimulatedPlantScenarioService(options);

        var cycle = simulation.Advance();

        Assert.True(cycle.DesiredPumpStates["tower"]);
        Assert.Equal(0m, cycle.State.LitersPerMinute);
        Assert.Contains(cycle.Events, item => item.Contains("no-flow", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Simulated_Esp32_Blocks_Valve_When_Associated_Container_Is_Full()
    {
        var options = CreateOptions();
        options.Simulation = new SimulationOptions
        {
            Enabled = true,
            Scenario = "ValveLock",
            CycleSeconds = 2,
            EnableValveLockScenario = true,
            AutoToggleValves = true
        };
        var simulation = new SimulatedPlantScenarioService(options);
        var client = new SimulatedEsp32Client(options, simulation);

        for (var index = 0; index < 5; index++)
        {
            simulation.Advance();
        }

        var result = await client.SendValveCommandAsync("esp32-a", 2, AgentCommandType.Open, CancellationToken.None);
        var snapshot = await client.GetStatusAsync("esp32-a", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("locked", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(snapshot.Valves, valve => valve.ValveId == 2 && valve.IsLocked);
    }

    [Fact]
    public void Calculates_No_Flow_Alert_When_Pump_Runs_Without_Pulses()
    {
        var flow = FlowCalculator.Calculate(100, 100, 450, TimeSpan.FromSeconds(2), pumpRunning: true, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(6));

        Assert.True(flow.NoFlowAlert);
        Assert.Equal(0m, flow.LitersPerMinute);
    }

    private static TlalocAgentOptions CreateOptions() => new()
    {
        Tower = new ReservoirHardwareOptions { LevelSensorPins = [5, 6, 13, 19, 26], PumpOutputPin = 17, MinLevelToRun = 2, UnlockLevel = 3 },
        Cistern = new ReservoirHardwareOptions { LevelSensorPins = [12, 16, 20, 21, 25], PumpOutputPin = 27, MinLevelToRun = 2, MaxLevelToRun = 5, UnlockLevel = 3 },
        Esp32Boards =
        [
            new Esp32BoardOptions { BoardId = "esp32-a", ControlsContainers = [1, 2, 3, 4, 5, 6, 7, 8], ControlsValves = [1, 2], ContainerStatusInputPinsOnRaspberry = [4, 18, 22, 24] },
            new Esp32BoardOptions { BoardId = "esp32-b", ControlsContainers = [9, 10, 11, 12, 13, 14, 15, 16], ControlsValves = [3, 4], ContainerStatusInputPinsOnRaspberry = [10, 9, 11, 8] }
        ]
    };

    private static SystemSnapshot CreateSnapshot(
        IReadOnlyList<bool>? towerSensors = null,
        IReadOnlyList<bool>? cisternSensors = null,
        IReadOnlyList<ValveSnapshot>? valves = null)
    {
        towerSensors ??= [true, true, true, false, false];
        cisternSensors ??= [true, true, true, false, false];
        valves ??=
        [
            new ValveSnapshot(1, false, false, null),
            new ValveSnapshot(2, false, false, null),
            new ValveSnapshot(3, false, false, null),
            new ValveSnapshot(4, false, false, null)
        ];

        return new SystemSnapshot(
            DateTime.UtcNow,
            new ReservoirSnapshot("tower", towerSensors, ReservoirLevelCalculator.Calculate(towerSensors, new ReservoirOptions(2, 3))),
            new ReservoirSnapshot("cistern", cisternSensors, ReservoirLevelCalculator.Calculate(cisternSensors, new ReservoirOptions(2, 3, 5))),
            new FlowSnapshot(0, 0, 0, false),
            [new PumpSnapshot("tower", false, false, null), new PumpSnapshot("cistern", false, false, null)],
            valves,
            Enumerable.Range(1, 16).Select(index => new ContainerSnapshot(index, false)).ToList(),
            [],
            [],
            new Dictionary<string, bool>());
    }
}

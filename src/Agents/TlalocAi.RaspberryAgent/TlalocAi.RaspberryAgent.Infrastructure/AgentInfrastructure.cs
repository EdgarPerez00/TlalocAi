using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using TlalocAi.RaspberryAgent.Application;
using TlalocAi.RaspberryAgent.Domain;

namespace TlalocAi.RaspberryAgent.Infrastructure;

public static class AgentInfrastructureExtensions
{
    public static IServiceCollection AddRaspberryAgentInfrastructure(this IServiceCollection services, TlalocAgentOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<ITelemetryQueue, OfflineTelemetryQueueService>();
        services.AddSingleton<ISafetyEvaluationService, DefaultSafetyEvaluationService>();
        services.AddSingleton<FlowSensorService>();
        services.AddSingleton<PumpControlService>();
        services.AddSingleton<ValveCommandService>();
        services.AddSingleton<SensorPollingService>();
        services.AddSingleton<TelemetryPublisherService>();
        services.AddSingleton<HeartbeatService>();
        services.AddSingleton<CommandPollingService>();

        if (IsSimulation(options))
        {
            services.AddSingleton<SimulatedGpioState>();
            services.AddSingleton<IGpioInputReader, SimulatedGpioInputReader>();
            services.AddSingleton<IGpioOutputWriter, SimulatedGpioOutputWriter>();
            services.AddSingleton<IFlowPulseCounter, SimulatedFlowPulseCounter>();
            services.AddSingleton<IEsp32Client, SimulatedEsp32Client>();
        }
        else
        {
            services.AddSingleton<IGpioInputReader, LinuxSysfsGpioInputReader>();
            services.AddSingleton<IGpioOutputWriter, LinuxSysfsGpioOutputWriter>();
            services.AddSingleton<IFlowPulseCounter, LinuxFlowPulseCounter>();
            services.AddSingleton<IEsp32Client, LinuxSerialEsp32Client>();
        }

        if (IsSimulation(options) && options.Backend.BaseUrl.Contains("TU_BACKEND", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IBackendClient, SimulatedBackendClient>();
        }
        else
        {
            services.AddSingleton(new HttpClient { BaseAddress = new Uri(options.Backend.BaseUrl.TrimEnd('/') + "/") });
            services.AddSingleton<IBackendClient, HttpBackendClient>();
        }

        return services;
    }

    private static bool IsSimulation(TlalocAgentOptions options) =>
        options.Agent.Mode.Equals("Simulation", StringComparison.OrdinalIgnoreCase);
}

public sealed class SimulatedGpioState
{
    private readonly ConcurrentDictionary<int, bool> _inputs = new();
    private readonly ConcurrentDictionary<int, bool> _outputs = new();

    public bool ReadInput(int pin) => _inputs.TryGetValue(pin, out var value) && value;
    public bool ReadOutput(int pin) => _outputs.TryGetValue(pin, out var value) && value;
    public void SetInput(int pin, bool value) => _inputs[pin] = value;
    public void SetOutput(int pin, bool value) => _outputs[pin] = value;
}

public sealed class SimulatedGpioInputReader : IGpioInputReader
{
    private readonly SimulatedGpioState _state;

    public SimulatedGpioInputReader(SimulatedGpioState state, TlalocAgentOptions options)
    {
        _state = state;
        foreach (var pin in options.Tower.LevelSensorPins.Concat(options.Cistern.LevelSensorPins))
        {
            _state.SetInput(pin, true);
        }

        foreach (var pin in options.Esp32Boards.SelectMany(board => board.ContainerStatusInputPinsOnRaspberry))
        {
            _state.SetInput(pin, false);
        }
    }

    public Task<bool> ReadAsync(int pin, CancellationToken cancellationToken) =>
        Task.FromResult(_state.ReadInput(pin));
}

public sealed class SimulatedGpioOutputWriter(SimulatedGpioState state) : IGpioOutputWriter
{
    public Task WriteAsync(int pin, bool isOn, CancellationToken cancellationToken)
    {
        state.SetOutput(pin, isOn);
        return Task.CompletedTask;
    }

    public bool GetLastState(int pin) => state.ReadOutput(pin);
}

public sealed class SimulatedFlowPulseCounter : IFlowPulseCounter
{
    private long _pulses;

    public Task<long> GetPulsesAsync(CancellationToken cancellationToken)
    {
        _pulses += 45;
        return Task.FromResult(_pulses);
    }
}

public sealed class SimulatedEsp32Client(TlalocAgentOptions options) : IEsp32Client
{
    private readonly Dictionary<string, BoardState> _boards = options.Esp32Boards.ToDictionary(
        board => board.BoardId,
        board => new BoardState(board.ControlsContainers, board.ControlsValves),
        StringComparer.OrdinalIgnoreCase);

    public Task<Esp32BoardSnapshot> GetStatusAsync(string boardId, CancellationToken cancellationToken)
    {
        if (!_boards.TryGetValue(boardId, out var board))
        {
            throw new InvalidOperationException($"Unknown simulated ESP32 board '{boardId}'.");
        }

        return Task.FromResult(board.ToSnapshot(boardId));
    }

    public Task<Esp32CommandResult> SendValveCommandAsync(string boardId, int localValveId, AgentCommandType commandType, CancellationToken cancellationToken)
    {
        if (!_boards.TryGetValue(boardId, out var board))
        {
            return Task.FromResult(new Esp32CommandResult(false, $"Unknown simulated ESP32 board '{boardId}'."));
        }

        var result = commandType == AgentCommandType.Open
            ? board.OpenValve(localValveId)
            : board.CloseValve(localValveId);

        return Task.FromResult(new Esp32CommandResult(result.Success, result.Message, board.ToSnapshot(boardId)));
    }

    private sealed class BoardState
    {
        private readonly int[] _containerIds;
        private readonly int[] _valveIds;
        private readonly bool[] _containerFull;
        private readonly bool[] _valveOpen;
        private readonly bool[] _valveLocked;

        public BoardState(int[] containerIds, int[] valveIds)
        {
            _containerIds = containerIds;
            _valveIds = valveIds;
            _containerFull = new bool[containerIds.Length];
            _valveOpen = new bool[valveIds.Length];
            _valveLocked = new bool[valveIds.Length];
        }

        public (bool Success, string Message) OpenValve(int localValveId)
        {
            var index = localValveId - 1;
            if (index < 0 || index >= _valveIds.Length)
            {
                return (false, "INVALID_VALVE");
            }

            UpdateLocks(index);
            if (_valveLocked[index])
            {
                _valveOpen[index] = false;
                return (false, "VALVE_LOCKED_OR_CONTAINER_FULL");
            }

            _valveOpen[index] = true;
            return (true, "Valve opened.");
        }

        public (bool Success, string Message) CloseValve(int localValveId)
        {
            var index = localValveId - 1;
            if (index < 0 || index >= _valveIds.Length)
            {
                return (false, "INVALID_VALVE");
            }

            _valveOpen[index] = false;
            return (true, "Valve closed.");
        }

        public Esp32BoardSnapshot ToSnapshot(string boardId)
        {
            for (var index = 0; index < _valveIds.Length; index++)
            {
                UpdateLocks(index);
            }

            var containers = _containerIds.Select((id, index) => new ContainerSnapshot(id, _containerFull[index])).ToList();
            var valves = _valveIds.Select((id, index) => new ValveSnapshot(id, _valveOpen[index], _valveLocked[index], _valveLocked[index] ? "Valve locked by simulated container fill." : null)).ToList();
            return new Esp32BoardSnapshot(boardId, containers, valves, true);
        }

        private void UpdateLocks(int valveIndex)
        {
            var first = valveIndex * 2;
            var second = first + 1;
            var anyFull = IsContainerFull(first) || IsContainerFull(second);
            var bothEmpty = !IsContainerFull(first) && !IsContainerFull(second);

            if (anyFull)
            {
                _valveLocked[valveIndex] = true;
                _valveOpen[valveIndex] = false;
            }
            else if (bothEmpty)
            {
                _valveLocked[valveIndex] = false;
            }
        }

        private bool IsContainerFull(int index) => index >= 0 && index < _containerFull.Length && _containerFull[index];
    }
}

public sealed class LinuxSysfsGpioInputReader : IGpioInputReader
{
    public async Task<bool> ReadAsync(int pin, CancellationToken cancellationToken)
    {
        await EnsureExportedAsync(pin, "in", cancellationToken);
        var valuePath = $"/sys/class/gpio/gpio{pin}/value";
        var value = await File.ReadAllTextAsync(valuePath, cancellationToken);
        return value.Trim() == "1";
    }

    internal static async Task EnsureExportedAsync(int pin, string direction, CancellationToken cancellationToken)
    {
        var gpioPath = $"/sys/class/gpio/gpio{pin}";
        if (!Directory.Exists(gpioPath))
        {
            await File.WriteAllTextAsync("/sys/class/gpio/export", pin.ToString(), cancellationToken);
        }

        var directionPath = Path.Combine(gpioPath, "direction");
        if (File.Exists(directionPath))
        {
            await File.WriteAllTextAsync(directionPath, direction, cancellationToken);
        }
    }
}

public sealed class LinuxSysfsGpioOutputWriter : IGpioOutputWriter
{
    private readonly ConcurrentDictionary<int, bool> _lastStates = new();

    public async Task WriteAsync(int pin, bool isOn, CancellationToken cancellationToken)
    {
        await LinuxSysfsGpioInputReader.EnsureExportedAsync(pin, "out", cancellationToken);
        await File.WriteAllTextAsync($"/sys/class/gpio/gpio{pin}/value", isOn ? "1" : "0", cancellationToken);
        _lastStates[pin] = isOn;
    }

    public bool GetLastState(int pin) => _lastStates.TryGetValue(pin, out var value) && value;
}

public sealed class LinuxFlowPulseCounter(IGpioInputReader inputReader, TlalocAgentOptions options) : IFlowPulseCounter
{
    private bool _lastState;
    private long _pulses;

    public async Task<long> GetPulsesAsync(CancellationToken cancellationToken)
    {
        var current = await inputReader.ReadAsync(options.FlowSensor.Pin, cancellationToken);
        if (current && !_lastState)
        {
            _pulses++;
        }

        _lastState = current;
        return _pulses;
    }
}

public sealed class LinuxSerialEsp32Client(TlalocAgentOptions options) : IEsp32Client
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Esp32BoardSnapshot> GetStatusAsync(string boardId, CancellationToken cancellationToken)
    {
        var board = ResolveBoard(boardId);
        var response = await SendSerialCommandAsync(board.SerialPort, "STATUS", cancellationToken);
        var dto = JsonSerializer.Deserialize<Esp32StatusDto>(response, JsonOptions) ?? throw new InvalidOperationException("Invalid ESP32 status JSON.");
        return ToSnapshot(board, dto);
    }

    public async Task<Esp32CommandResult> SendValveCommandAsync(string boardId, int localValveId, AgentCommandType commandType, CancellationToken cancellationToken)
    {
        var board = ResolveBoard(boardId);
        var verb = commandType == AgentCommandType.Open ? "OPEN" : "CLOSE";
        var response = await SendSerialCommandAsync(board.SerialPort, $"{verb} {localValveId}", cancellationToken);
        var result = JsonSerializer.Deserialize<Esp32CommandDto>(response, JsonOptions);
        return new Esp32CommandResult(result?.Ok == true, result?.Error ?? result?.Action ?? response);
    }

    private Esp32BoardOptions ResolveBoard(string boardId) =>
        options.Esp32Boards.Single(board => board.BoardId.Equals(boardId, StringComparison.OrdinalIgnoreCase));

    private static async Task<string> SendSerialCommandAsync(string serialPort, string command, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(serialPort, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        await using var writer = new StreamWriter(stream) { AutoFlush = true, NewLine = "\n" };
        using var reader = new StreamReader(stream);
        await writer.WriteLineAsync(command.AsMemory(), cancellationToken);
        var readTask = reader.ReadLineAsync(cancellationToken).AsTask();
        var completed = await Task.WhenAny(readTask, Task.Delay(TimeSpan.FromSeconds(2), cancellationToken));
        if (completed != readTask)
        {
            throw new TimeoutException($"ESP32 serial command timed out on {serialPort}.");
        }

        return await readTask ?? throw new InvalidOperationException("ESP32 returned an empty serial response.");
    }

    private static Esp32BoardSnapshot ToSnapshot(Esp32BoardOptions board, Esp32StatusDto dto)
    {
        var containers = board.ControlsContainers.Select((id, index) => new ContainerSnapshot(id, dto.Containers.ElementAtOrDefault(index) == 1)).ToList();
        var valves = dto.Valves.Select(valve =>
        {
            var globalValve = board.ControlsValves.ElementAtOrDefault(valve.Index - 1);
            return new ValveSnapshot(globalValve, valve.Open, valve.Locked, valve.Locked ? "Valve locked by ESP32." : null);
        }).Where(item => item.ValveId > 0).ToList();

        return new Esp32BoardSnapshot(board.BoardId, containers, valves, true);
    }

    private sealed record Esp32StatusDto(string BoardId, int[] Containers, Esp32ValveDto[] Valves);
    private sealed record Esp32ValveDto(int Index, bool Open, bool Locked);
    private sealed record Esp32CommandDto(bool Ok, string? Action, string? Error);
}

public sealed class HttpBackendClient(HttpClient httpClient, TlalocAgentOptions options) : IBackendClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task SendHeartbeatAsync(HeartbeatPayload heartbeat, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/devices/{options.Agent.DeviceId}/heartbeat")
        {
            Content = JsonContent.Create(heartbeat, options: JsonOptions)
        };

        AddDeviceApiKey(request);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task PublishTelemetryAsync(SystemSnapshot snapshot, CancellationToken cancellationToken)
    {
        var payload = ToTelemetryPayload(snapshot);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/devices/{options.Agent.DeviceId}/telemetry")
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };

        AddDeviceApiKey(request);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<PendingDeviceCommand>> GetPendingCommandsAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/devices/{options.Agent.DeviceId}/commands/pending");
        AddDeviceApiKey(request);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var commands = await response.Content.ReadFromJsonAsync<List<PendingCommandDto>>(JsonOptions, cancellationToken) ?? [];
        return commands.Select(ToPendingCommand).ToList();
    }

    public async Task AckCommandAsync(CommandExecutionResult result, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/devices/{options.Agent.DeviceId}/commands/{result.CommandId}/ack")
        {
            Content = JsonContent.Create(new { success = true, message = result.Message, executedAtUtc = result.ExecutedAtUtc }, options: JsonOptions)
        };

        AddDeviceApiKey(request);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RejectCommandAsync(CommandExecutionResult result, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/devices/{options.Agent.DeviceId}/commands/{result.CommandId}/reject")
        {
            Content = JsonContent.Create(new { reason = result.Message, executedAtUtc = result.ExecutedAtUtc }, options: JsonOptions)
        };

        AddDeviceApiKey(request);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private void AddDeviceApiKey(HttpRequestMessage request) =>
        request.Headers.TryAddWithoutValidation("X-Device-Api-Key", options.Backend.ApiKey);

    private static PendingDeviceCommand ToPendingCommand(PendingCommandDto dto)
    {
        var targetType = ParseTargetType(dto.TargetType, dto.Target);
        var commandType = ParseCommandType(dto.CommandType, targetType, dto.State);
        var targetId = dto.TargetId ?? dto.Target.Replace("pump_", string.Empty, StringComparison.OrdinalIgnoreCase).Replace("valve_", string.Empty, StringComparison.OrdinalIgnoreCase);
        return new PendingDeviceCommand(dto.CommandId, targetType, targetId, commandType, dto.Payload);
    }

    private static CommandTargetType ParseTargetType(string? targetType, string target)
    {
        if (Enum.TryParse<CommandTargetType>(targetType, true, out var parsed))
        {
            return parsed;
        }

        return target.StartsWith("valve_", StringComparison.OrdinalIgnoreCase) ? CommandTargetType.Valve : CommandTargetType.Pump;
    }

    private static AgentCommandType ParseCommandType(string? commandType, CommandTargetType targetType, bool state)
    {
        if (Enum.TryParse<AgentCommandType>(commandType, true, out var parsed))
        {
            return parsed;
        }

        return targetType == CommandTargetType.Valve
            ? state ? AgentCommandType.Open : AgentCommandType.Close
            : state ? AgentCommandType.Start : AgentCommandType.Stop;
    }

    private static object ToTelemetryPayload(SystemSnapshot snapshot) => new
    {
        timestampUtc = snapshot.TimestampUtc,
        tower = ToReservoir(snapshot.Tower),
        cistern = ToReservoir(snapshot.Cistern),
        flow = new
        {
            litersPerMinute = snapshot.Flow.LitersPerMinute,
            totalLiters = snapshot.Flow.TotalLiters,
            pulses = snapshot.Flow.Pulses,
            noFlowAlert = snapshot.Flow.NoFlowAlert
        },
        pumps = snapshot.Pumps.Select(pump => new { pumpId = pump.PumpId, isOn = pump.IsOn, isBlocked = pump.IsBlocked, blockReason = pump.BlockReason }),
        valves = snapshot.Valves.Select(valve => new { valveId = valve.ValveId, isOpen = valve.IsOpen, isLocked = valve.IsLocked, lockReason = valve.LockReason }),
        containers = snapshot.Containers.Select(container => new { containerId = container.ContainerId, isFull = container.IsFull }),
        faults = snapshot.Faults,
        warnings = snapshot.Warnings,
        rawInputs = snapshot.RawInputs
    };

    private static object ToReservoir(ReservoirSnapshot reservoir) => new
    {
        name = reservoir.Name,
        level = reservoir.Evaluation.Level,
        sensors = reservoir.Sensors,
        isCritical = reservoir.Evaluation.IsCritical,
        hasInvalidReading = reservoir.Evaluation.HasInvalidReading,
        message = reservoir.Message
    };

    private sealed record PendingCommandDto(Guid CommandId, string Type, string Target, bool State, DateTime CreatedAtUtc, string? TargetType, string? TargetId, string? CommandType, string? Payload);
}

public sealed class SimulatedBackendClient : IBackendClient
{
    private readonly Queue<PendingDeviceCommand> _pendingCommands = new();
    public List<SystemSnapshot> PublishedTelemetry { get; } = [];
    public List<CommandExecutionResult> CommandResults { get; } = [];

    public Task SendHeartbeatAsync(HeartbeatPayload heartbeat, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task PublishTelemetryAsync(SystemSnapshot snapshot, CancellationToken cancellationToken)
    {
        PublishedTelemetry.Add(snapshot);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PendingDeviceCommand>> GetPendingCommandsAsync(CancellationToken cancellationToken)
    {
        var commands = _pendingCommands.ToList();
        _pendingCommands.Clear();
        return Task.FromResult<IReadOnlyList<PendingDeviceCommand>>(commands);
    }

    public Task AckCommandAsync(CommandExecutionResult result, CancellationToken cancellationToken)
    {
        CommandResults.Add(result);
        return Task.CompletedTask;
    }

    public Task RejectCommandAsync(CommandExecutionResult result, CancellationToken cancellationToken)
    {
        CommandResults.Add(result);
        return Task.CompletedTask;
    }

    public void Enqueue(PendingDeviceCommand command) => _pendingCommands.Enqueue(command);
}

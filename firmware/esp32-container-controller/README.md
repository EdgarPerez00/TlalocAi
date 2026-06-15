# TlalocAi ESP32 Container Controller

This firmware is built with PlatformIO and Arduino for two variants:

- `esp32-a`: containers 1 to 4, valves 1 and 2.
- `esp32-b`: containers 5 to 8, valves 3 and 4.

Build examples:

```bash
pio run -e esp32-a
pio run -e esp32-b
```

Serial protocol at `115200` baud:

- `OPEN 1`
- `CLOSE 1`
- `OPEN 2`
- `CLOSE 2`
- `STATUS`

Each board uses the same physical pins. For board B the Raspberry Agent maps local valves 1 and 2 to global valves 3 and 4.

## Pins

Container sensors:

- Container local 1 sensor A: GPIO32
- Container local 1 sensor B: GPIO33
- Container local 2 sensor A: GPIO34
- Container local 2 sensor B: GPIO35
- Container local 3 sensor A: GPIO36
- Container local 3 sensor B: GPIO39
- Container local 4 sensor A: GPIO27
- Container local 4 sensor B: GPIO14

Status outputs to Raspberry:

- Local container 1 status: GPIO16
- Local container 2 status: GPIO17
- Local container 3 status: GPIO18
- Local container 4 status: GPIO19

Valve outputs:

- Local valve 1: GPIO25 through MOSFET or isolated driver
- Local valve 2: GPIO26 through MOSFET or isolated driver

## Safety

The conversion table is fixed:

- `00` -> not full
- `01` -> not full
- `10` -> full
- `11` -> full

If any container associated with a valve is full, the valve closes and locks. It unlocks only when both associated containers return to not full. `CLOSE` is always accepted; `OPEN` is rejected while locked.

## Electrical notes

- Do not connect pumps or valves directly to GPIO.
- Use external power drivers and an external actuator supply.
- Join Raspberry, ESP32 and actuator supply GND when using common DC switching.
- Adapt any 5V sensor output to 3.3V before Raspberry or ESP32 input.
- Add flyback diodes for DC inductive loads.
- For AC pumps use an adequate contactor, SSR or isolated module. Do not handle mains wiring without proper protection.

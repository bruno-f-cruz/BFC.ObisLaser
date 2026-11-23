---
title: Commands
---

# Commands

`CreateCommand` creates the commands sent by `Device`. Select a command with the `Command` property; its parameters, such as `Watts` for `SetPower`, appear below it in the property grid.

- **Without an input**, `CreateCommand` creates one command when the workflow starts.
- **With an input**, it creates one command for each input notification.
- `Channel` selects the laser: 0 for a laser connected directly, 1 or more for the lasers connected to an OBIS Remote or LaserBox.

Each command captures its parameter values when it is created. Invalid values, such as a negative power, raise an error when the command is created, before anything is sent.

## Change the power while a workflow runs

Parameters can be mapped from upstream values, like any Bonsai property. This workflow sends a new `SetPower` command whenever `Watts` changes:

:::workflow
![Power setpoint](~/workflows/03-power-setpoint.bonsai)
:::

[Download the workflow](~/workflows/03-power-setpoint.bonsai).

1. `DoubleProperty` emits its `Value` when the workflow starts and whenever it changes. `Value` is externalized as `Watts`, so it can be edited on the workflow while it runs.
2. `InputMapping` copies each value into the `Watts` parameter of `CreateCommand`, then triggers it.
3. `CreateCommand` creates a `SetPower` command with the new value, and `Device` sends it.

Replace `DoubleProperty` with any source of numbers, such as a sensor or a behavior event, to control the power online. `Device` sends commands one at a time: each command waits for the reply to the previous one, or for `Timeout`. Commands arriving in the meantime are queued in order; up to `MaxPendingCommands` can wait, and more raise an error so that stale setpoints never pile up.

## Available commands

Units follow the laser: power in watts, current in amperes and temperatures in degrees Celsius. Commands marked persistent are stored in the laser. Some commands only exist on some lasers (OBIS LX, OBIS LS or the OBIS Remote); other lasers reject them with an error reply.

### Information

| Command | Parameters | Reply value | Description |
| --- | --- | --- | --- |
| `Identify` | — | `string` | Gets the identification string (*IDN?). |
| `GetInfo` | — | `DeviceInfo` | Gets the model, serial number, firmware, wavelength, power limits and other static information. |
| `GetUserInfo` | `Index` | `string` | Gets the user-defined text stored at an index. |
| `SetUserInfo` | `Index`, `Text` | — | Persistently stores user-defined text at an index. |
| `GetFieldCalibrationDate` | — | `string` | Gets the date of the last field calibration. |
| `SetFieldCalibrationDate` | `Text` | — | Persistently stores the date of the last field calibration. |

### Control

| Command | Parameters | Reply value | Description |
| --- | --- | --- | --- |
| `GetEmission` | — | `bool` | Gets whether emission is turned on. |
| `SetEmission` | `Enabled` | — | Turns emission on or off. Emission may be delayed by warm-up and the CDRH delay. |
| `GetPowerSetpoint` | — | `double` | Gets the requested output power, in watts. |
| `SetPower` | `Watts` | — | Sets the output power, in watts. Does not turn emission on. |
| `GetPower` | — | `double` | Gets the measured output power, in watts. |
| `GetCurrent` | — | `double` | Gets the laser diode current, in amperes. |
| `GetOperatingMode` | — | `OperatingMode` | Gets the operating mode. |
| `SetOperatingMode` | `Mode` | — | Persistently sets the operating mode. |

### State

| Command | Parameters | Reply value | Description |
| --- | --- | --- | --- |
| `GetState` | — | `LaserState` | Gets the emission, operating mode, power, current, base plate temperature, status and faults. |
| `GetStatus` | — | `StatusFlags` | Gets the system status word. |
| `GetFaults` | — | `FaultFlags` | Gets the active faults. A reset or power cycle is needed to clear them. |
| `GetBaseplateTemperature` | — | `double` | Gets the base plate temperature, in degrees Celsius. |
| `GetDiodeTemperature` | — | `double` | Gets the laser diode temperature, in degrees Celsius (OBIS LX). |
| `GetDiodeTemperatureSetpoint` | — | `double` | Gets the TEC set point of the laser diode, in degrees Celsius (OBIS LX). |
| `GetInternalTemperature` | — | `double` | Gets the internal temperature, in degrees Celsius (OBIS LX). |
| `GetBaseplateTemperatureLimits` | — | `TemperatureLimits` | Gets the base plate temperature range outside of which the laser faults. |
| `GetDiodeTemperatureLimits` | — | `TemperatureLimits` | Gets the diode temperature range outside of which the laser faults (OBIS LX). |
| `GetInternalTemperatureLimits` | — | `TemperatureLimits` | Gets the internal temperature range outside of which the laser faults (OBIS LX). |
| `GetThresholdCurrent` | — | `double` | Gets the laser diode threshold current, in amperes. |
| `GetMaxCurrent` | — | `double` | Gets the laser diode upper current limit, in amperes (OBIS LX, firmware 2.x or later). |
| `GetInterlock` | — | `bool` | Gets the OBIS Remote interlock state: true when closed (emission allowed). |
| `GetNoise` | — | `int` | Gets the relative power noise level (constant power mode). Above 30 is considered noisy. |
| `GetPowerCycles` | — | `int` | Gets the number of times the device has been powered on. |
| `GetHours` | — | `double` | Gets the number of hours the device has been powered on. |
| `GetDiodeHours` | — | `double` | Gets the number of hours the laser diode has been emitting. |

### Error queue

| Command | Parameters | Reply value | Description |
| --- | --- | --- | --- |
| `GetErrors` | — | `IReadOnlyList<ErrorRecord>` | Reads and removes every record of the device error queue, oldest first. |
| `ClearErrors` | — | — | Clears the device error queue. |

### Settings

| Command | Parameters | Reply value | Description |
| --- | --- | --- | --- |
| `GetAutoStart` | — | `bool` | Gets whether the laser starts emitting at power-up. |
| `SetAutoStart` | `Enabled` | — | Persistently sets whether the laser starts emitting at power-up. |
| `GetCdrhDelay` | — | `bool` | Gets whether the 5 s CDRH emission delay is enabled. |
| `SetCdrhDelay` | `Enabled` | — | Persistently enables or disables the 5 s CDRH emission delay. |
| `GetTecEnabled` | — | `bool` | Gets whether the TEC (LX) or thermal stabilization (LS) is enabled. |
| `SetTecEnabled` | `Enabled` | — | Enables or disables the TEC (LX) or thermal stabilization (LS). |
| `GetDiodeWarmup` | — | `bool` | Gets whether emission waits for the diode warm-up (OBIS LX). |
| `SetDiodeWarmup` | `Enabled` | — | Persistently sets whether emission waits for the diode warm-up (OBIS LX). |
| `GetStatusIndicator` | — | `bool` | Gets whether the status LED is enabled. |
| `SetStatusIndicator` | `Enabled` | — | Persistently turns the status LED on or off; status bits are not affected. |
| `GetAnalogBlanking` | — | `bool` | Gets whether blanking is enabled in analog modulation modes. |
| `SetAnalogBlanking` | `Enabled` | — | Enables or disables blanking in analog modulation modes. |
| `GetAnalogImpedance` | — | `AnalogImpedance` | Gets the OBIS Remote analog input impedance. |
| `SetAnalogImpedance` | `Impedance` | — | Persistently selects the OBIS Remote analog input impedance. |

### Maintenance

| Command | Parameters | Reply value | Description |
| --- | --- | --- | --- |
| `Reset` | — | — | Warm-boots the device. Clears fault conditions. |
| `SelfTest` | — | `FaultFlags` | Runs the self-test. 0xFFFFFFFF means the self-test is not implemented. |
| `StartFieldCalibration` | — | — | Starts a power field calibration (OBIS LX). Takes minutes; do not cut power. |
| `UndoFieldCalibration` | — | — | Undoes the last field calibration (OBIS LX). |
| `Recover` | — | — | Recovers from a persistent checksum error, restoring factory defaults. Ask Coherent before using it. |

### Raw commands

| Command | Parameters | Reply value | Description |
| --- | --- | --- | --- |
| `Query` | `Text` | `IReadOnlyList<string>` | Sends any command or query text and returns the reply lines. |
| `Broadcast` | `Text` | — | Sends a command to every laser connected to an OBIS Remote or LaserBox. The channel is ignored. |

`Query` and `Broadcast` send any command text, such as commands of newer firmware. The text is sent as is, apart from the channel.

Continue with [replies and errors](replies.md).

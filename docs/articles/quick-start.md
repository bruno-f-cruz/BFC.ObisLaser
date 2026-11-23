---
title: Read and control a laser
---

# Read and control a laser

This walkthrough reads the state of a laser every second, then sets its power and turns emission on.

## Read the laser state

Hover over the workflow below and click its copy button, then paste it into an empty Bonsai workflow.

:::workflow
![Read the laser state](~/workflows/01-read-state.bonsai)
:::

[Download the workflow](~/workflows/01-read-state.bonsai).

1. `Timer` triggers `CreateCommand` immediately, then every second.
2. `CreateCommand`, with `Command` set to `GetState`, creates a command for each trigger.
3. `Device` opens the serial port in `PortName`, sends each command and emits its reply. Each command waits for the reply to the previous one.
4. `ParseReply`, with `Command` set to `GetState`, emits the `LaserState` of each reply.

Select `Device` and set `PortName` to the port of your laser; the drop-down lists the ports of this computer. Press **F5** to start and double-click `ParseReply` to see the emission, operating mode, power, current, base plate temperature, status and faults. Stop with **Shift+F5**; the port is closed when the workflow stops.

## Set the power and turn emission on

> [!WARNING]
> This workflow turns emission on. Make sure the beam is safely blocked.

:::workflow
![Turn the laser on](~/workflows/02-turn-on.bonsai)
:::

[Download the workflow](~/workflows/02-turn-on.bonsai).

1. Three `CreateCommand` nodes without inputs each create one command when the workflow starts: `SetOperatingMode` (constant power), `SetPower` (10 mW) and `SetEmission` (on).
2. `Concat` sends them to `Device` in that order.
3. `EnsureSuccess` stops the workflow with an error if the laser rejects any of them, for example a power above its maximum.

Emission may start a few seconds after `SetEmission`, because of the laser warm-up and the 5 s CDRH delay. Emission stays on after the workflow stops. To turn it off, change `Enabled` to `False` on the `SetEmission` node and run the workflow again.

## If it does not run

| Symptom | What to check |
| --- | --- |
| `Specify the serial port of the laser in PortName` | Set `PortName` on `Device` |
| Access to the port is denied | Another program, such as Coherent Connection or another workflow, has the port open |
| The sequence times out | The laser is off, on another port, or behind a controller: set `Channel` on `CreateCommand` |
| `ParseReply` emits nothing | The command was rejected: inspect the replies of `Device`, or add `EnsureSuccess` |

Continue with [commands](commands.md).

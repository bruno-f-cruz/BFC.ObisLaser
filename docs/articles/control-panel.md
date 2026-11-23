---
title: Control panel
---

# Control panel

`ObisLaserVisualizer` shows a dashboard for one laser while a workflow runs: what the laser is, its live readings, controls for emission, power and operating mode, its persistent settings, its faults, and a log of every reply. The dashboard needs the **BFC.ObisLaser.Design** package; install it in your Bonsai environment along with BFC.ObisLaser.

## Connect the panel

:::workflow
![Control panel](~/workflows/07-control-panel.bonsai)
:::

[Download the workflow](~/workflows/07-control-panel.bonsai).

The panel receives replies and sends commands, so it connects to `Device` through the subjects described in [Several lasers and devices](connections.md):

1. `PublishSubject` declares the `LaserCommands` subject, of type `ObisCommand`.
2. `SubscribeSubject` feeds the commands sent to `LaserCommands` into `Device`, which publishes the replies to `LaserReplies`.
3. `ObisLaserVisualizer` receives the replies from `LaserReplies`, and sends the commands requested from the panel to `LaserCommands`.

Set `PortName` on `Device` and `Channel` on `ObisLaserVisualizer`: 0 for a laser connected directly, 1 or more for a laser behind an OBIS Remote or LaserBox. Start the workflow and double-click `ObisLaserVisualizer` to open the panel. The panel requests the device information and state when it opens.

## Use the dashboard

- **Header**: the wavelength, in its color, with the model, serial number and channel. The badge shows whether the laser is emitting, in standby, in a fault, in its CDRH delay or warming up. **Live** requests the state every `PollInterval` (1 s by default) to keep the readings current; set `PollState` on the node to start it on when the workflow starts. **Refresh** reads everything again.
- **Control**: the **Emission** switch turns emission on and off; emission stays on after the workflow stops. Drag the **Power** slider, which runs from 0 to the maximum power of the laser, or type a power in milliwatts; the power is sent when you release the slider or leave the box. The **Mode** pills select the operating mode, such as CWP for constant power.
- **Readings**: output power, setpoint, diode current, base plate temperature (amber within 3 °C of its limits, red beyond), operating hours and mode.
- **Device information**: model, wavelength, serial and part numbers, firmware, power ratings, manufacture and calibration dates, hours, power cycles, currents and temperatures.
- **Settings**: auto start, CDRH delay, diode warm-up, TEC, status indicator, analog blanking and the analog input impedance. These are stored in the laser and persist after power cycles.
- **Status and faults**: the status and fault flags of the laser, its error queue, and **Recover** when the laser is in a fault.
- **Messages**: every reply, newest last: the command, the value or the error, and how long the exchange took. Rejected commands are shown in red.

Values the laser does not support, which depends on its model and firmware, are shown as "Not supported", and settings it does not support are hidden. Each button sends its command and then requests the state, so the display reflects the change. The panel shows only replies to commands for its channel, including commands sent by other parts of the workflow, so its log also shows what the rest of the workflow does to the laser. Commands from the panel share the queue of `Device` with every other command.

To control several lasers, add one `ObisLaserVisualizer` per laser, each with its own `Channel`.

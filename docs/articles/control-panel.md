---
title: Control panel
---

# Control panel

`ObisLaserVisualizer` shows a control panel for one laser while a workflow runs: its state, buttons to turn emission on and off, set the power and the operating mode, and a collapsible log of every reply. The panel needs the **BFC.ObisLaser.Design** package; install it in your Bonsai environment along with BFC.ObisLaser.

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

## Use the panel

- **Emission ON** and **Emission OFF** turn emission on and off. Emission stays on after the workflow stops.
- **Set power** sends the power in the box, in milliwatts. Powers above the laser maximum are flagged before sending.
- **Set mode** sends the operating mode selected, such as CWP for constant power.
- **Refresh** requests the device information and state. **Poll state** requests the state every `PollInterval` (1 s by default) to keep the display live; set `PollState` on the node to start polling when the workflow starts.
- **Messages** expands the log of replies, newest last: the command, the value or the error, and how long the exchange took. Rejected commands and invalid parameters are shown in red.

Each button sends its command and then requests the state, so the display reflects the change. The panel shows only replies to commands for its channel, including commands sent by other parts of the workflow, so its log also shows what the rest of the workflow does to the laser. Commands from the panel share the queue of `Device` with every other command.

To control several lasers, add one `ObisLaserVisualizer` per laser, each with its own `Channel`.

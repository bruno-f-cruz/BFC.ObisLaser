---
uid: overview
---

BFC.ObisLaser lets you control Coherent OBIS LX/LS lasers from Bonsai: read device information and state, set power and operating mode, turn emission on and off, and change persistent settings.

Every interaction with a laser is a message. `CreateCommand` creates a command, `Device` sends it to the laser and emits the reply, and `ParseReply` extracts typed values from replies. This is the same pattern as Harp devices. For example, this workflow reads the laser state every second:

:::workflow
![Read the laser state](~/workflows/01-read-state.bonsai)
:::

`Timer` triggers `CreateCommand`, which creates a `GetState` command; `Device` sends it to the laser on its serial port and emits the reply; `ParseReply` emits the `LaserState` it contains.

These guides assume the package is installed in your Bonsai environment and that the laser is connected over USB or RS-232. Over USB, Windows shows the laser or controller as a serial port such as `COM3`, without any extra driver on current Windows versions.

## Guides

- [Read and control a laser](quick-start.md): read the laser state, then set the power and turn emission on.
- [Commands](commands.md): the available commands, and how to change parameters such as the power while a workflow runs.
- [Replies and errors](replies.md): typed values, rejected commands and timeouts.
- [Several lasers and devices](connections.md): send commands from several places, control several lasers, and find connected devices.
- [Control panel](control-panel.md): control a laser and watch its replies from a panel while the workflow runs.

## Safety

Commands act on real hardware. Emission stays on after a workflow stops: send `SetEmission` with `Enabled` set to `False` to turn it off. Commands documented as persistent, such as `SetAutoStart` or `SetOperatingMode`, are stored in the laser and survive power cycles.

OBIS and Coherent are trademarks of Coherent Corp. This project is not affiliated with or endorsed by Coherent.

---
uid: overview
---

BFC.ObisLaser lets you control Coherent OBIS LX/LS lasers from Bonsai: read device information and state, set power and operating mode, turn emission on and off, and change persistent settings.

Every interaction with a laser is a message. `CreateCommand` creates a command, `SendCommand` sends it to the laser and emits the reply, and `ParseReply` extracts typed values from replies:

```text
Timer → CreateCommand → SendCommand → ParseReply
        (GetState)      (COM3)        (GetState → LaserState)
```

These guides assume the package is installed in your Bonsai environment and that the laser is connected over USB or RS-232. Coherent's USB driver, installed with Coherent Connection, makes the laser appear as a serial port such as `COM3`.

## Guides

- [Read and control a laser](quick-start.md): read the laser state, then set the power and turn emission on.
- [Commands](commands.md): the available commands, and how to change parameters such as the power while a workflow runs.
- [Replies and errors](replies.md): typed values, rejected commands and timeouts.
- [Connections and devices](connections.md): share a port between several nodes, control several lasers, and find connected devices.

## Safety

Commands act on real hardware. Emission stays on after a workflow stops: send `SetEmission` with `Enabled` set to `False` to turn it off. Commands documented as persistent, such as `SetAutoStart` or `SetOperatingMode`, are stored in the laser and survive power cycles.

OBIS and Coherent are trademarks of Coherent Corp. This project is not affiliated with or endorsed by Coherent.

# BFC.ObisLaser

Bonsai operators to control Coherent OBIS LX/LS lasers over USB or RS-232: read device information and state, set power and operating mode, turn emission on and off, and change persistent settings. Lasers connected through an OBIS Remote or LaserBox are addressed by channel.

The package uses a message-based design:

- `CreateCommand` creates commands. Select the command from a drop-down, such as `SetPower`, `SetEmission`, `GetState` or `GetInfo`; its parameters appear alongside it and can be mapped from upstream values.
- `SendCommand` sends commands one at a time and in order, and emits a reply for each. It opens the serial port itself, or uses a connection shared from `CreateObisConnection` through a `ResourceSubject`.
- `ParseReply` emits the typed values of one kind of reply, such as `LaserState` for `GetState`.
- `EnsureSuccess` raises an error for commands rejected by the laser, which are otherwise emitted as data.
- `FindDevices` lists the OBIS lasers and controllers connected to the computer.

The operators are built on [ObisLaser.Net](https://github.com/bruno-f-cruz/ObisLaser.Net) and target .NET Framework 4.7.2 and .NET 8.

See the [documentation](https://bruno-f-cruz.github.io/BFC.ObisLaser/) for guides and example workflows.

OBIS and Coherent are trademarks of Coherent Corp. This project is not affiliated with or endorsed by Coherent.

# BFC.ObisLaser

Bonsai operators to control Coherent OBIS LX/LS lasers over USB or RS-232: read device information and state, set power and operating mode, turn emission on and off, and change persistent settings. Lasers connected through an OBIS Remote or LaserBox are addressed by channel.

The package uses a message-based design:

- `CreateCommand` creates commands. Select the command from a drop-down, such as `SetPower`, `SetEmission`, `GetState` or `GetInfo`; its parameters appear alongside it and can be mapped from upstream values.
- `Device` opens the serial port of a laser or controller, sends each command and emits its reply, which contains the command. Commands are sent one at a time: each waits for the previous reply, or for the configurable `Timeout`, and commands arriving in the meantime are queued in order.
- `ParseReply` emits the typed values of one kind of reply, such as `LaserState` for `GetState`.
- `EnsureSuccess` raises an error for commands rejected by the laser, which are otherwise emitted as data.
- `FindDevices` lists the OBIS lasers and controllers connected to the computer.
- `ObisLaserVisualizer` shows a dashboard for a laser while the workflow runs: wavelength and device information, live readings, an emission switch, a power slider, operating mode, persistent settings, faults and a log of the replies. The dashboard is in the BFC.ObisLaser.Design package.

The operators are built on [ObisLaser.Net](https://github.com/bruno-f-cruz/ObisLaser.Net) and target .NET Framework 4.7.2 and .NET 8.

See the [documentation](https://bruno-f-cruz.github.io/BFC.ObisLaser/) for guides and example workflows.

OBIS and Coherent are trademarks of Coherent Corp. This project is not affiliated with or endorsed by Coherent.

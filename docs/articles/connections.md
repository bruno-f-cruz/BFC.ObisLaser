---
title: Several lasers and devices
---

# Several lasers and devices

A serial port can only be opened once, so each port has exactly one `Device` node. To send commands from several places in a workflow, use the same pattern as Harp devices: send every command to one subject that feeds the `Device`, and publish the replies through another subject.

## Send commands from several places

:::workflow
![Several lasers](~/workflows/04-several-lasers.bonsai)
:::

[Download the workflow](~/workflows/04-several-lasers.bonsai).

1. `PublishSubject` declares the `LaserCommands` subject, of type `ObisCommand`.
2. `SubscribeSubject` feeds every command sent to `LaserCommands` into `Device`, which publishes the replies to `LaserReplies`.
3. Each `CreateCommand` sends its commands to `LaserCommands` with `MulticastSubject`. Here, two timers read the power of lasers 1 and 2.
4. Each `ParseReply` subscribes to `LaserReplies` and keeps the replies to its command and channel.

`Device` sends the commands one at a time, in the order they reach `LaserCommands`: each waits for the reply to the previous one. Each reply contains the command it answers, so replies can always be traced back to their command, parameters and channel.

## Lasers behind a controller

Channel 0 is the device on the port: the laser itself when it is connected directly, or the controller. Lasers connected to an OBIS Remote or LaserBox are numbered from 1; set `Channel` on `CreateCommand` to address them, and on `ParseReply` to keep only their replies. Use the `Broadcast` command to send a command to all of them at once, for example `SOUR:AM:STAT ON` to turn them all on.

Lasers on different ports each need their own `Device`.

## Find connected devices

:::workflow
![Find devices](~/workflows/05-find-devices.bonsai)
:::

[Download the workflow](~/workflows/05-find-devices.bonsai).

`FindDevices` sends an identification query to each serial port and emits each OBIS laser or controller found, with its port, channel, model and serial number. Lasers behind a controller are listed with their channel.

OBIS lasers and controllers connected over USB are recognized by Coherent's USB vendor ID without opening any port, so `FindDevices` probes only their ports, or every serial port if there are none. Devices connected over RS-232, for example through a USB adapter, cannot be recognized this way: set `AllPorts` to probe every serial port. The `PortName` list of `Device` shows the same ports, but accepts any port name.

Probing writes to each port, which may disturb other serial devices. Set `PortNames` to probe only some ports, or `ExcludedPortNames` to skip some. Ports already in use are skipped, so run it while no other workflow is connected to the lasers.

---
title: Connections and devices
---

# Connections and devices

A serial port can only be opened once. With one input, `SendCommand` opens the port in `PortName` itself, which is the simplest choice when one node talks to a laser. When several nodes talk to the same port, share one connection instead.

## Share a connection

`CreateObisConnection` opens the port. Store the connection in a `ResourceSubject`, and connect a `SubscribeSubject` with the same name to the second input of each `SendCommand`:

:::workflow
![Shared connection](~/workflows/04-shared-connection.bonsai)
:::

[Download the workflow](~/workflows/04-shared-connection.bonsai).

This workflow reads the power of lasers 1 and 2, connected to an OBIS Remote or LaserBox on one port. Each `CreateCommand` sets its `Channel`. Commands from all the senders are sent one at a time, so their replies never mix. The `ResourceSubject` closes the port when the workflow stops.

With a shared connection, `SendCommand` ignores its own `PortName` and `Timeout`; set them on `CreateObisConnection`.

## Lasers behind a controller

Channel 0 is the device on the port: the laser itself when it is connected directly, or the controller. Lasers connected to an OBIS Remote or LaserBox are numbered from 1. Use the `Broadcast` command to send a command to all of them at once, for example `SOUR:AM:STAT ON` to turn them all on.

## Find connected devices

:::workflow
![Find devices](~/workflows/05-find-devices.bonsai)
:::

[Download the workflow](~/workflows/05-find-devices.bonsai).

`FindDevices` sends an identification query to each serial port and emits each OBIS laser or controller found, with its port, channel, model and serial number. Lasers behind a controller are listed with their channel.

Probing writes to every serial port, which may disturb other serial devices. Set `PortNames` to probe only some ports, or `ExcludedPortNames` to skip some. Ports already in use are skipped, so run it while no other workflow is connected to the lasers.

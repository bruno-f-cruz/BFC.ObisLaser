---
title: Replies and errors
---

# Replies and errors

`SendCommand` emits one `ObisReply` for each command, in the order the commands were sent.

| Property | What it contains |
| --- | --- |
| `Command` | The command this reply answers, with its name, parameters and channel |
| `Value` | The value read from the laser, or null for commands that only report success |
| `IsSuccess` | Whether the laser accepted the command |
| `ErrorCode`, `Error` | The error returned by the laser, for rejected commands |
| `Duration` | The time taken to send the command and receive its reply |

## Get typed values

`ParseReply` emits the values of successful replies to one kind of command, and ignores all other replies. The selected command determines the output type:

| `ParseReply.Command` | Output |
| --- | --- |
| `GetState` | `LaserState` |
| `GetInfo` | `DeviceInfo` |
| `GetPower`, `GetCurrent`, temperatures | `double` |
| `GetStatus` | `StatusFlags` |
| `GetFaults` | `FaultFlags` |

See [commands](commands.md) for the reply value of every command. Because it filters by command, one `SendCommand` can serve several `ParseReply` nodes, for example one for `GetPower` and one for `GetFaults`. Stop and restart after changing `Command`, since the output type changes.

## Rejected commands

The laser rejects commands it cannot execute: a power above its maximum, a command it does not support, or a setting that conflicts with its state. `SendCommand` emits these as replies with `IsSuccess` set to `False` and the laser's error code, such as `-220` for an invalid parameter, so that one invalid setting does not stop a running experiment.

To stop on any rejection instead, place `EnsureSuccess` after `SendCommand`:

```text
SendCommand → EnsureSuccess → ParseReply
```

## Errors that stop the workflow

These always raise a sequence error:

- **No reply within `Timeout`**: the laser is off, disconnected, or on another channel.
- **The port cannot be opened**: it does not exist or another program is using it.
- **Too many pending commands**: more than `MaxPendingCommands` commands waiting to be sent.
- **Invalid command parameters**, such as a negative power, raised by `CreateCommand` before anything is sent.

## Laser faults

Faults are reported by the laser itself, not as errors: `GetFaults` returns the active faults, and `LaserState.Faults` includes them. Send `Reset` to clear them once the cause is fixed.

Continue with [connections and devices](connections.md).

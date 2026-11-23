using System.ComponentModel;
using ObisLaser.Net;

namespace BFC.ObisLaser;

/// <summary>
/// The reply to an <see cref="ObisCommand"/>, emitted by <see cref="SendCommand"/>.
/// </summary>
/// <remarks>
/// A command rejected by the laser (an <c>ERR</c> reply) is emitted as a reply with
/// <see cref="IsSuccess"/> set to <see langword="false"/>, so the workflow keeps running.
/// Use <see cref="EnsureSuccess"/> to raise an error instead.
/// </remarks>
public sealed class ObisReply
{
    public ObisReply(ObisCommand command, object? value, ObisDeviceException? error, TimeSpan duration)
    {
        Command = command ?? throw new ArgumentNullException(nameof(command));
        Value = value;
        Error = error;
        Duration = duration;
    }

    [Description("The command this reply answers.")]
    public ObisCommand Command { get; }

    [Description("The value read from the laser, or null for commands that only report success and for failed commands. Use ParseReply to obtain typed values.")]
    public object? Value { get; }

    [Description("The error returned by the laser, or null if the command succeeded.")]
    public ObisDeviceException? Error { get; }

    [Description("Indicates whether the laser accepted the command.")]
    public bool IsSuccess => Error is null;

    [Description("The error code returned by the laser (manual Table 9-7), or null if the command succeeded.")]
    public int? ErrorCode => Error?.Code;

    [Description("The time taken to send the command and receive its reply.")]
    public TimeSpan Duration { get; }

    public override string ToString() => IsSuccess
        ? Value is null ? $"{Command}: OK" : $"{Command}: {Value}"
        : $"{Command}: ERR{Error!.Code} {Error.Description}";
}

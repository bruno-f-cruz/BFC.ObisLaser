using System.ComponentModel;
using ObisLaser.Net;

namespace BFC.ObisLaser;

/// <summary>
/// An immutable command for one OBIS laser, created by <see cref="CreateCommand"/> and executed by
/// <see cref="SendCommand"/>.
/// </summary>
/// <remarks>
/// Parameter values, such as the power of a <see cref="SetPower"/> command, are captured when the
/// command is created, so later changes to the creating node do not affect commands already sent.
/// </remarks>
public sealed class ObisCommand
{
    readonly Func<Laser, object?> execute;

    internal ObisCommand(string name, string text, int channel, Type? resultType, Func<Laser, object?> execute)
    {
        if (channel < 0 || channel >= ObisProtocol.BroadcastChannel)
            throw new ArgumentOutOfRangeException(nameof(channel), channel, "The channel must be between 0 and 254.");
        Name = name;
        Text = text;
        Channel = channel;
        ResultType = resultType;
        this.execute = execute;
    }

    [Description("The name of the command, such as SetPower or GetState.")]
    public string Name { get; }

    [Description("The command and its parameters, such as SetPower(0.01 W).")]
    public string Text { get; }

    [Description("The laser channel: 0 for a laser connected directly, 1 or more for lasers behind an OBIS Remote or LaserBox.")]
    public int Channel { get; }

    [Description("The type of the reply value, or null for commands that only report success.")]
    public Type? ResultType { get; }

    internal object? Execute(Laser laser) => execute(laser);

    public override string ToString() => Channel == 0 ? Text : $"{Text} @ channel {Channel}";
}

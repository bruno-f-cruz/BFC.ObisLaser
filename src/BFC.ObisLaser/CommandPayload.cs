using ObisLaser.Net;

namespace BFC.ObisLaser;

/// <summary>
/// The base class of the commands selectable in <see cref="CreateCommand"/>, such as <see cref="SetPower"/>
/// or <see cref="GetState"/>.
/// </summary>
public abstract class CommandPayload
{
    private protected CommandPayload()
    {
    }

    internal string Name => GetType().Name;

    internal abstract Type? ResultType { get; }

    /// <summary>
    /// Creates the command for the laser on <paramref name="channel"/>, to be sent to a <see cref="Device"/>.
    /// Parameter values are captured now, as in <see cref="BFC.ObisLaser.CreateCommand"/>.
    /// </summary>
    /// <param name="channel">The laser channel: 0 for a laser connected directly, 1 or more for lasers behind an OBIS
    /// Remote or LaserBox.</param>
    /// <returns>The command.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The channel is not between 0 and 254.</exception>
    public abstract ObisCommand CreateCommand(int channel);

    public override string ToString() => Name;
}

/// <summary>
/// The base class of commands whose reply carries a value of type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The type of the reply value.</typeparam>
public abstract class QueryPayload<T> : CommandPayload
{
    private protected QueryPayload()
    {
    }

    internal override Type? ResultType => typeof(T);

    public override ObisCommand CreateCommand(int channel)
    {
        var query = CreateQuery();
        return new ObisCommand(Name, ToString(), channel, typeof(T), laser => query(laser));
    }

    // Called when the command is created: validates and captures the current parameter values.
    private protected abstract Func<Laser, T> CreateQuery();
}

/// <summary>
/// The base class of commands whose reply only reports success.
/// </summary>
public abstract class ActionPayload : CommandPayload
{
    private protected ActionPayload()
    {
    }

    internal override Type? ResultType => null;

    public override ObisCommand CreateCommand(int channel)
    {
        var action = CreateAction();
        return new ObisCommand(Name, ToString(), channel, null, laser =>
        {
            action(laser);
            return null;
        });
    }

    // Called when the command is created: validates and captures the current parameter values.
    private protected abstract Action<Laser> CreateAction();
}

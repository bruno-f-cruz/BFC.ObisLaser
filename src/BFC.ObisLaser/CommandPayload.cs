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

    internal abstract ObisCommand CreateCommand(int channel);

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

    internal override ObisCommand CreateCommand(int channel)
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

    internal override ObisCommand CreateCommand(int channel)
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

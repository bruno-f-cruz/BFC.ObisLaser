using System.Reactive.Subjects;
using ObisLaser.Net;

namespace BFC.ObisLaser;

/// <summary>
/// Connects a running <see cref="ObisLaserVisualizer"/> to its panel: keeps the latest values and a
/// log of the replies received, and emits the commands requested from the panel.
/// </summary>
/// <remarks>Replies arrive on workflow threads while the panel reads from the UI thread, so all state is locked.</remarks>
internal sealed class ObisLaserSession
{
    /// <summary>The maximum number of entries kept in the message log.</summary>
    public const int LogCapacity = 500;

    readonly object gate = new();
    readonly Subject<ObisCommand> commands = new();
    readonly LinkedList<SessionLogEntry> log = new();
    LaserState? state;
    DeviceInfo? info;
    bool pollPending;
    volatile bool polling;

    public ObisLaserSession(int channel) => Channel = channel;

    /// <summary>The channel of the laser.</summary>
    public int Channel { get; }

    /// <summary>Whether the laser state is requested periodically.</summary>
    public bool Polling
    {
        get => polling;
        set => polling = value;
    }

    /// <summary>The commands requested from the panel.</summary>
    public IObservable<ObisCommand> Commands => commands;

    /// <summary>The latest laser state received, if any.</summary>
    public LaserState? State
    {
        get { lock (gate) return state; }
    }

    /// <summary>The latest device information received, if any.</summary>
    public DeviceInfo? Info
    {
        get { lock (gate) return info; }
    }

    /// <summary>Returns a copy of the message log, oldest first.</summary>
    public SessionLogEntry[] GetLog()
    {
        lock (gate) return log.ToArray();
    }

    /// <summary>Removes every entry from the message log.</summary>
    public void ClearLog()
    {
        lock (gate) log.Clear();
    }

    /// <summary>Requests a command for the laser of this session.</summary>
    /// <returns><see langword="false"/> if the command could not be created, e.g. with an out-of-range parameter.</returns>
    public bool Send(CommandPayload payload)
    {
        ObisCommand command;
        try
        {
            command = payload.CreateCommand(Channel);
        }
        catch (ArgumentException error)
        {
            var message = new StringReader(error.Message).ReadLine();
            Append(new SessionLogEntry(DateTime.Now, payload.ToString(), null, message, null));
            return false;
        }

        lock (commands) commands.OnNext(command);
        return true;
    }

    /// <summary>Requests the laser state if polling is on and the previous request was answered.</summary>
    public void Poll()
    {
        if (!polling) return;
        lock (gate)
        {
            if (pollPending) return;
            pollPending = true;
        }

        if (!Send(new GetState()))
        {
            lock (gate) pollPending = false;
        }
    }

    /// <summary>Records a reply.</summary>
    public void Receive(ObisReply reply)
    {
        lock (gate)
        {
            switch (reply.Value)
            {
                case LaserState value:
                    state = value;
                    break;
                case DeviceInfo value:
                    info = value;
                    break;
            }

            if (reply.Command.Name == nameof(GetState)) pollPending = false;
        }

        Append(new SessionLogEntry(
            DateTime.Now,
            reply.Command.Text,
            reply.IsSuccess ? FormatValue(reply.Value) : null,
            reply.Error?.Message,
            reply.Duration));
    }

    void Append(SessionLogEntry entry)
    {
        lock (gate)
        {
            log.AddLast(entry);
            while (log.Count > LogCapacity) log.RemoveFirst();
        }
    }

    static string FormatValue(object? value) => value switch
    {
        null => "OK",
        double number => number.ToString("G6", System.Globalization.CultureInfo.InvariantCulture),
        System.Collections.IEnumerable items and not string => string.Join(", ", items.Cast<object>()),
        _ => value.ToString() ?? string.Empty,
    };
}

/// <summary>An entry of the message log of an <see cref="ObisLaserVisualizer"/> panel.</summary>
/// <param name="Time">The local time the reply was received.</param>
/// <param name="Command">The command, as text.</param>
/// <param name="Value">The value replied, or "OK"; null if the command failed.</param>
/// <param name="Error">The error message, if the command failed.</param>
/// <param name="Duration">The time the exchange took, if the command was sent.</param>
internal sealed record SessionLogEntry(DateTime Time, string Command, string? Value, string? Error, TimeSpan? Duration)
{
    public bool IsSuccess => Error is null;
}

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
    readonly Dictionary<string, object?> values = new();
    readonly HashSet<string> unsupported = new();
    DateTime? lastReplyTime;
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
    public LaserState? State => Get<LaserState>(nameof(GetState));

    /// <summary>The latest device information received, if any.</summary>
    public DeviceInfo? Info => Get<DeviceInfo>(nameof(GetInfo));

    /// <summary>The local time of the latest reply, if any.</summary>
    public DateTime? LastReplyTime
    {
        get { lock (gate) return lastReplyTime; }
    }

    /// <summary>Returns the latest value replied to a query, such as <c>GetHours</c>, or null if there is none.</summary>
    public T? Get<T>(string command) where T : class
    {
        lock (gate) return values.TryGetValue(command, out var value) ? value as T : null;
    }

    /// <summary>Returns the latest value replied to a query of a value type, or null if there is none.</summary>
    public T? GetValue<T>(string command) where T : struct
    {
        lock (gate) return values.TryGetValue(command, out var value) && value is T result ? result : null;
    }

    /// <summary>
    /// Returns whether the laser does not support a query: it was rejected as an unrecognized command,
    /// or answered with a reply that cannot be read. Such queries are not sent again.
    /// </summary>
    public bool IsUnsupported(string command)
    {
        lock (gate) return unsupported.Contains(command);
    }

    /// <summary>Requests several commands in order.</summary>
    public void Send(params CommandPayload[] payloads)
    {
        foreach (var payload in payloads)
        {
            if (!Send(payload)) return;
        }
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
        if (IsUnsupported(payload.Name)) return true;

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
            lastReplyTime = DateTime.Now;
            if (reply.IsSuccess && reply.Command.ResultType is not null) values[reply.Command.Name] = reply.Value;
            if (reply.Command.ResultType is not null && reply.Command.Name != nameof(Query) && IsUnsupportedReply(reply)) unsupported.Add(reply.Command.Name);
            if (reply.Command.Name == nameof(GetState)) pollPending = false;
        }

        Append(new SessionLogEntry(
            DateTime.Now,
            reply.Command.Text,
            reply.IsSuccess ? FormatValue(reply.Value) : null,
            reply.Error?.Message,
            reply.Duration));
    }

    // ERR-100 is "Unrecognized command or query"; some firmware also answers queries it does not support without a value.
    static bool IsUnsupportedReply(ObisReply reply) => reply.ErrorCode == UnrecognizedCommand || reply.Error is ObisProtocolException;

    const int UnrecognizedCommand = -100;

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

using System.ComponentModel;
using System.Diagnostics;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Xml;
using System.Xml.Serialization;
using Bonsai;
using ObisLaser.Net;

namespace BFC.ObisLaser;

/// <summary>
/// Opens the serial port of an OBIS laser or controller, sends each command and emits its reply.
/// </summary>
/// <remarks>
/// Commands are sent one at a time: each command waits for the reply to the previous one, or for
/// <see cref="Timeout"/>. Commands arriving in the meantime are queued in order; more than 64 waiting
/// commands raise an error, so that stale commands never pile up. The port is closed
/// when the workflow stops. To send commands from several places, merge them into one subject that
/// feeds this node, and publish the replies through another subject.
/// Commands rejected by the laser, or answered with a reply that cannot be read, are emitted as replies
/// with <see cref="ObisReply.IsSuccess"/> set to <see langword="false"/>; timeouts and communication
/// errors raise a sequence error.
/// Before the port is closed, emission is turned off: on the device on the port, on every channel
/// commands were sent to, and, through a controller, on every laser connected to it.
/// </remarks>
[Combinator]
[WorkflowElementCategory(ElementCategory.Source)]
[Description("Sends commands to an OBIS laser or controller one at a time, and emits the reply to each.")]
public sealed class Device
{
    [TypeConverter(typeof(PortNameConverter))]
    [Description("The serial port of the laser or controller, e.g. COM3. The list shows OBIS devices connected over USB, or every serial port if there are none. Read when the workflow starts.")]
    public string? PortName { get; set; }

    [XmlIgnore]
    [Description("The maximum time to wait for each reply. A timeout raises a sequence error. Read when the workflow starts.")]
    public TimeSpan Timeout { get; set; } = ObisConnection.DefaultTimeout;

    [Browsable(false)]
    [XmlElement(nameof(Timeout))]
    [Description("The XML duration used to serialize Timeout.")]
    public string TimeoutXml
    {
        get => XmlConvert.ToString(Timeout);
        set => Timeout = XmlConvert.ToTimeSpan(value);
    }

    // The maximum number of commands waiting for their turn. Exceeding it raises an error, so that
    // stale commands never pile up. Settable only for tests.
    internal int MaxPendingCommands { get; set; } = 64;

    public IObservable<ObisReply> Process(IObservable<ObisCommand> source) =>
        Process(source, () => Open(PortName, Timeout));

    internal IObservable<ObisReply> Process(IObservable<ObisCommand> source, Func<ObisConnection> open) =>
        Observable.Create<ObisReply>(observer =>
        {
            var maxPending = MaxPendingCommands;
            if (maxPending < 1) throw new InvalidOperationException("MaxPendingCommands must be positive.");

            var connection = open();
            var channels = new HashSet<int> { 0 };
            var closed = 0;
            void Close()
            {
                if (Interlocked.Exchange(ref closed, 1) != 0) return;
                try { TurnOffEmission(connection, Snapshot(channels)); }
                finally { connection.Dispose(); }
            }

            var pending = 0;
            var replies = source.Select(command =>
            {
                lock (channels) channels.Add(command.Channel);
                if (Interlocked.Increment(ref pending) > maxPending + 1)
                {
                    Interlocked.Decrement(ref pending);
                    throw new InvalidOperationException($"More than {maxPending} commands are waiting to be sent.");
                }

                return Observable.FromAsync(() => Task.Run(() => Execute(connection, command)))
                    .Finally(() => Interlocked.Decrement(ref pending));
            }).Merge(1); // One at a time and in order; unlike Concat, errors are raised immediately.

            // The port is closed before completion or errors are forwarded, so that operators such as
            // Repeat or Retry can open it again right away.
            var subscription = replies.Subscribe(
                observer.OnNext,
                error =>
                {
                    Close();
                    observer.OnError(error);
                },
                () =>
                {
                    Close();
                    observer.OnCompleted();
                });
            return new CompositeDisposable(subscription, Disposable.Create(Close));
        });

    static int[] Snapshot(HashSet<int> channels)
    {
        lock (channels) return channels.OrderBy(channel => channel).ToArray();
    }

    /// <summary>
    /// Turns emission off on each channel, then broadcasts it to every laser behind a controller if the
    /// device on the port is a controller or commands were sent to lasers behind one.
    /// </summary>
    /// <remarks>
    /// Waits for any command in progress, since the connection exchanges one message at a time.
    /// Rejected commands are ignored; the first timeout or communication error stops, since the device
    /// no longer answers.
    /// </remarks>
    internal static void TurnOffEmission(ObisConnection connection, IReadOnlyList<int> channels)
    {
        var controller = channels.Any(channel => channel > 0);
        try
        {
            foreach (var channel in channels)
            {
                try { connection.GetLaser(channel).SetEmission(false); }
                catch (ObisDeviceException) { if (channel == 0) controller = true; }
            }

            if (controller) connection.Broadcast("SOUR:AM:STAT OFF");
        }
        catch (Exception error) when (error is ObisException or IOException or TimeoutException or InvalidOperationException or UnauthorizedAccessException)
        {
        }
    }

    static ObisConnection Open(string? portName, TimeSpan timeout)
    {
        if (string.IsNullOrWhiteSpace(portName)) throw new InvalidOperationException("Specify the serial port of the laser in PortName.");
        if (timeout <= TimeSpan.Zero) throw new InvalidOperationException("Timeout must be positive.");
        return ObisConnection.Open(portName!, timeout);
    }

    internal static ObisReply Execute(ObisConnection connection, ObisCommand command)
    {
        var laser = connection.GetLaser(command.Channel);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            var value = command.Execute(laser);
            return new ObisReply(command, value, null, stopwatch.Elapsed);
        }
        catch (Exception error) when (error is ObisDeviceException or ObisProtocolException)
        {
            return new ObisReply(command, null, (ObisException)error, stopwatch.Elapsed);
        }
    }
}

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
/// Commands rejected by the laser are emitted as replies with <see cref="ObisReply.IsSuccess"/> set to
/// <see langword="false"/>; timeouts and communication errors raise a sequence error.
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
            var pending = 0;
            var replies = source.Select(command =>
            {
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
                    connection.Dispose();
                    observer.OnError(error);
                },
                () =>
                {
                    connection.Dispose();
                    observer.OnCompleted();
                });
            return new CompositeDisposable(subscription, connection);
        });

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
        catch (ObisDeviceException error)
        {
            return new ObisReply(command, null, error, stopwatch.Elapsed);
        }
    }
}

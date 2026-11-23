using System.ComponentModel;
using System.Diagnostics;
using System.Reactive.Linq;
using System.Xml;
using System.Xml.Serialization;
using Bonsai;
using ObisLaser.Net;

namespace BFC.ObisLaser;

/// <summary>
/// Sends commands to OBIS lasers and emits their replies, one command at a time and in order.
/// </summary>
/// <remarks>
/// With one input, the node opens the serial port in <see cref="PortName"/> and closes it when the
/// workflow stops. With a second input, it uses a connection shared from <see cref="CreateObisConnection"/>
/// through a ResourceSubject instead, and ignores <see cref="PortName"/> and <see cref="Timeout"/>.
/// Commands rejected by the laser are emitted as replies with <see cref="ObisReply.IsSuccess"/> set to
/// <see langword="false"/>; timeouts and communication errors raise a sequence error.
/// </remarks>
[Combinator]
[WorkflowElementCategory(ElementCategory.Transform)]
[Description("Sends commands to OBIS lasers and emits their replies, one command at a time and in order.")]
public sealed class SendCommand
{
    [TypeConverter(typeof(PortNameConverter))]
    [Description("The serial port of the laser or controller, e.g. COM3. Unused with a shared connection. Read when the workflow starts.")]
    public string? PortName { get; set; }

    [XmlIgnore]
    [Description("The maximum time to wait for each reply. A timeout raises a sequence error. Unused with a shared connection. Read when the workflow starts.")]
    public TimeSpan Timeout { get; set; } = ObisConnection.DefaultTimeout;

    [Browsable(false)]
    [XmlElement(nameof(Timeout))]
    [Description("The XML duration used to serialize Timeout.")]
    public string TimeoutXml
    {
        get => XmlConvert.ToString(Timeout);
        set => Timeout = XmlConvert.ToTimeSpan(value);
    }

    [Description("The maximum number of commands waiting to be sent. Exceeding it raises an error, so that stale commands never pile up. Read when the workflow starts.")]
    public int MaxPendingCommands { get; set; } = 64;

    public IObservable<ObisReply> Process(IObservable<ObisCommand> source) =>
        Observable.Using(() => CreateObisConnection.Open(PortName, Timeout), connection => Process(source, Observable.Return(connection)));

    public IObservable<ObisReply> Process(IObservable<ObisCommand> source, IObservable<ObisConnection> connections) => Observable.Defer(() =>
    {
        var maxPending = MaxPendingCommands;
        if (maxPending < 1) throw new InvalidOperationException("MaxPendingCommands must be positive.");

        // Commands are accepted immediately and wait for the connection, which may arrive later.
        return connections.Take(1).DefaultIfEmpty()
            .Select(connection => connection ?? throw new InvalidOperationException("The connection source completed without a connection."))
            .Replay(shared =>
            {
                var pending = 0;
                var replies = source.Select(command =>
                {
                    if (Interlocked.Increment(ref pending) > maxPending + 1)
                    {
                        Interlocked.Decrement(ref pending);
                        throw new InvalidOperationException($"More than {maxPending} commands are waiting to be sent.");
                    }

                    return shared
                        .SelectMany(connection => Observable.FromAsync(() => Task.Run(() => Execute(connection, command))))
                        .Finally(() => Interlocked.Decrement(ref pending));
                }).Merge(1); // One at a time and in order; unlike Concat, errors are raised immediately.
                return replies.Merge(shared.SelectMany(_ => Observable.Empty<ObisReply>()));
            }, 1);
    });

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

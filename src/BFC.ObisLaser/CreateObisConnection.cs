using System.ComponentModel;
using System.Reactive.Linq;
using System.Xml;
using System.Xml.Serialization;
using Bonsai;
using ObisLaser.Net;

namespace BFC.ObisLaser;

/// <summary>
/// Opens a connection to an OBIS laser or controller, to be shared through a ResourceSubject.
/// </summary>
/// <remarks>
/// Share one connection when several <see cref="SendCommand"/> nodes control the same port, for
/// example lasers on different channels of an OBIS Remote or LaserBox. The ResourceSubject closes
/// the port when the workflow stops.
/// </remarks>
[Description("Opens a connection to an OBIS laser or controller, to be shared through a ResourceSubject.")]
public sealed class CreateObisConnection : Source<ObisConnection>
{
    [TypeConverter(typeof(PortNameConverter))]
    [Description("The serial port of the laser or controller, e.g. COM3. Read when the workflow starts.")]
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

    public override IObservable<ObisConnection> Generate() => Observable.Defer(() =>
        Observable.Return(Open(PortName, Timeout)));

    internal static ObisConnection Open(string? portName, TimeSpan timeout)
    {
        if (string.IsNullOrWhiteSpace(portName)) throw new InvalidOperationException("Specify the serial port of the laser in PortName.");
        if (timeout <= TimeSpan.Zero) throw new InvalidOperationException("Timeout must be positive.");
        return ObisConnection.Open(portName!, timeout);
    }
}

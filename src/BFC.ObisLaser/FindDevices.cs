using System.ComponentModel;
using System.Reactive.Linq;
using Bonsai;
using ObisLaser.Net;

namespace BFC.ObisLaser;

/// <summary>
/// Finds OBIS lasers and controllers by probing serial ports, and emits each device found.
/// </summary>
/// <remarks>
/// Each port is sent an identification query, which may disturb other serial devices; use
/// <see cref="PortNames"/> or <see cref="ExcludedPortNames"/> to limit the ports probed. Lasers
/// behind an OBIS Remote or LaserBox are listed with their channel. Ports in use are skipped, so
/// run this node while no workflow is connected to the lasers.
/// </remarks>
[Description("Finds OBIS lasers and controllers by probing serial ports.")]
public sealed class FindDevices : Source<DiscoveredDevice>
{
    [Description("The ports to probe, separated by commas, e.g. COM3,COM4. Leave empty to probe every serial port.")]
    public string? PortNames { get; set; }

    [Description("The ports never to probe, separated by commas, e.g. COM1.")]
    public string? ExcludedPortNames { get; set; }

    public override IObservable<DiscoveredDevice> Generate() => Observable.Defer(() =>
    {
        var ports = Split(PortNames);
        var excluded = Split(ExcludedPortNames);
        return Observable.FromAsync(() => Task.Run(() => ObisDiscovery.FindAll(ports.Length > 0 ? ports : null, excluded)))
            .SelectMany(devices => devices);
    });

    static string[] Split(string? names) =>
        (names ?? string.Empty).Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
}

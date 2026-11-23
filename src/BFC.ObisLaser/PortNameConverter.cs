using System.ComponentModel;
using System.IO.Ports;
using ObisLaser.Net;

namespace BFC.ObisLaser;

/// <summary>
/// Offers the serial ports of OBIS devices connected over USB in the property grid, or every serial
/// port if there are none, while accepting any port name.
/// </summary>
sealed class PortNameConverter : StringConverter
{
    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;

    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => false;

    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context)
    {
        var candidates = ObisDiscovery.GetCandidatePortNames();
        var portNames = candidates.Count > 0 ? candidates : SerialPort.GetPortNames();
        return new(portNames.OrderBy(name => name, StringComparer.Ordinal).ToArray());
    }
}

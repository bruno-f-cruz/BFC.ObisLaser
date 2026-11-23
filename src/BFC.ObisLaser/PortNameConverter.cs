using System.ComponentModel;
using System.IO.Ports;

namespace BFC.ObisLaser;

/// <summary>
/// Offers the serial ports of this computer in the property grid, while accepting any port name.
/// </summary>
sealed class PortNameConverter : StringConverter
{
    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;

    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => false;

    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context) =>
        new(SerialPort.GetPortNames().OrderBy(name => name, StringComparer.Ordinal).ToArray());
}

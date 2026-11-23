// The commands selectable in CreateCommand. Each command captures its parameter values when it is
// created, so later changes (for example through property mapping) only affect new commands.
using System.ComponentModel;
using ObisLaser.Net;

namespace BFC.ObisLaser;

// -------------------- Information -----------------------------------------------------------

/// <summary>Gets the identification string (*IDN?).</summary>
[Description("Gets the identification string (*IDN?).")]
public sealed class Identify : QueryPayload<string>
{
    private protected override Func<Laser, string> CreateQuery() => laser => laser.Identify();
}

/// <summary>Gets the model, serial number, firmware, wavelength, power limits and other static information.</summary>
[Description("Gets the model, serial number, firmware, wavelength, power limits and other static information.")]
public sealed class GetInfo : QueryPayload<DeviceInfo>
{
    private protected override Func<Laser, DeviceInfo> CreateQuery() => laser => laser.GetInfo();
}

/// <summary>Gets the user-defined text stored at an index.</summary>
[Description("Gets the user-defined text stored at an index.")]
public sealed class GetUserInfo : QueryPayload<string>
{
    [Description("The storage index, from 0 to 3.")]
    public int Index { get; set; }

    private protected override Func<Laser, string> CreateQuery()
    {
        var index = Index;
        return laser => laser.GetUserInfo(index);
    }

    public override string ToString() => $"GetUserInfo({Index})";
}

/// <summary>Persistently stores user-defined text at an index.</summary>
[Description("Persistently stores user-defined text at an index.")]
public sealed class SetUserInfo : ActionPayload
{
    [Description("The storage index, from 0 to 3.")]
    public int Index { get; set; }

    [Description("The text to store, up to 31 ASCII characters.")]
    public string Text { get; set; } = "";

    private protected override Action<Laser> CreateAction()
    {
        var index = Index;
        var text = Text ?? string.Empty;
        return laser => laser.SetUserInfo(index, text);
    }

    public override string ToString() => $"SetUserInfo({Index}, {Text})";
}

/// <summary>Gets the date of the last field calibration.</summary>
[Description("Gets the date of the last field calibration.")]
public sealed class GetFieldCalibrationDate : QueryPayload<string>
{
    private protected override Func<Laser, string> CreateQuery() => laser => laser.GetFieldCalibrationDate();
}

/// <summary>Persistently stores the date of the last field calibration.</summary>
[Description("Persistently stores the date of the last field calibration.")]
public sealed class SetFieldCalibrationDate : ActionPayload
{
    [Description("The date, up to 31 ASCII characters.")]
    public string Text { get; set; } = "";

    private protected override Action<Laser> CreateAction()
    {
        var text = Text ?? string.Empty;
        return laser => laser.SetFieldCalibrationDate(text);
    }

    public override string ToString() => $"SetFieldCalibrationDate({Text})";
}

// -------------------- Control ---------------------------------------------------------------

/// <summary>Gets whether emission is turned on.</summary>
[Description("Gets whether emission is turned on.")]
public sealed class GetEmission : QueryPayload<bool>
{
    private protected override Func<Laser, bool> CreateQuery() => laser => laser.GetEmission();
}

/// <summary>Turns emission on or off. Emission may be delayed by warm-up and the CDRH delay.</summary>
[Description("Turns emission on or off. Emission may be delayed by warm-up and the CDRH delay.")]
public sealed class SetEmission : ActionPayload
{
    [Description("Whether to turn emission on.")]
    public bool Enabled { get; set; }

    private protected override Action<Laser> CreateAction()
    {
        var enabled = Enabled;
        return laser => laser.SetEmission(enabled);
    }

    public override string ToString() => $"SetEmission({Enabled})";
}

/// <summary>Gets the requested output power, in watts.</summary>
[Description("Gets the requested output power, in watts.")]
public sealed class GetPowerSetpoint : QueryPayload<double>
{
    private protected override Func<Laser, double> CreateQuery() => laser => laser.GetPowerSetpoint();
}

/// <summary>Sets the output power, in watts. Does not turn emission on.</summary>
[Description("Sets the output power, in watts. Does not turn emission on.")]
public sealed class SetPower : ActionPayload
{
    [Description("The output power, in watts.")]
    public double Watts { get; set; }

    private protected override Action<Laser> CreateAction()
    {
        var request = ObisProtocol.SetPower(Watts);
        return laser => laser.Execute(request);
    }

    public override string ToString() => $"SetPower({Watts} W)";
}

/// <summary>Gets the measured output power, in watts.</summary>
[Description("Gets the measured output power, in watts.")]
public sealed class GetPower : QueryPayload<double>
{
    private protected override Func<Laser, double> CreateQuery() => laser => laser.GetPower();
}

/// <summary>Gets the laser diode current, in amperes.</summary>
[Description("Gets the laser diode current, in amperes.")]
public sealed class GetCurrent : QueryPayload<double>
{
    private protected override Func<Laser, double> CreateQuery() => laser => laser.GetCurrent();
}

/// <summary>Gets the operating mode.</summary>
[Description("Gets the operating mode.")]
public sealed class GetOperatingMode : QueryPayload<OperatingMode>
{
    private protected override Func<Laser, OperatingMode> CreateQuery() => laser => laser.GetOperatingMode();
}

/// <summary>Persistently sets the operating mode.</summary>
[Description("Persistently sets the operating mode.")]
public sealed class SetOperatingMode : ActionPayload
{
    [Description("The operating mode. External modes require an OBIS Remote.")]
    public OperatingMode Mode { get; set; } = OperatingMode.Cwp;

    private protected override Action<Laser> CreateAction()
    {
        var mode = Mode;
        return laser => laser.SetOperatingMode(mode);
    }

    public override string ToString() => $"SetOperatingMode({Mode})";
}

// -------------------- State -----------------------------------------------------------------

/// <summary>Gets the emission, operating mode, power, current, base plate temperature, status and faults.</summary>
[Description("Gets the emission, operating mode, power, current, base plate temperature, status and faults.")]
public sealed class GetState : QueryPayload<LaserState>
{
    private protected override Func<Laser, LaserState> CreateQuery() => laser => laser.GetState();
}

/// <summary>Gets the system status word.</summary>
[Description("Gets the system status word.")]
public sealed class GetStatus : QueryPayload<StatusFlags>
{
    private protected override Func<Laser, StatusFlags> CreateQuery() => laser => laser.GetStatus();
}

/// <summary>Gets the active faults. A reset or power cycle is needed to clear them.</summary>
[Description("Gets the active faults. A reset or power cycle is needed to clear them.")]
public sealed class GetFaults : QueryPayload<FaultFlags>
{
    private protected override Func<Laser, FaultFlags> CreateQuery() => laser => laser.GetFaults();
}

/// <summary>Gets the base plate temperature, in degrees Celsius.</summary>
[Description("Gets the base plate temperature, in degrees Celsius.")]
public sealed class GetBaseplateTemperature : QueryPayload<double>
{
    private protected override Func<Laser, double> CreateQuery() => laser => laser.GetBaseplateTemperature();
}

/// <summary>Gets the laser diode temperature, in degrees Celsius (OBIS LX).</summary>
[Description("Gets the laser diode temperature, in degrees Celsius (OBIS LX).")]
public sealed class GetDiodeTemperature : QueryPayload<double>
{
    private protected override Func<Laser, double> CreateQuery() => laser => laser.GetDiodeTemperature();
}

/// <summary>Gets the TEC set point of the laser diode, in degrees Celsius (OBIS LX).</summary>
[Description("Gets the TEC set point of the laser diode, in degrees Celsius (OBIS LX).")]
public sealed class GetDiodeTemperatureSetpoint : QueryPayload<double>
{
    private protected override Func<Laser, double> CreateQuery() => laser => laser.GetDiodeTemperatureSetpoint();
}

/// <summary>Gets the internal temperature, in degrees Celsius (OBIS LX).</summary>
[Description("Gets the internal temperature, in degrees Celsius (OBIS LX).")]
public sealed class GetInternalTemperature : QueryPayload<double>
{
    private protected override Func<Laser, double> CreateQuery() => laser => laser.GetInternalTemperature();
}

/// <summary>Gets the base plate temperature range outside of which the laser faults.</summary>
[Description("Gets the base plate temperature range outside of which the laser faults.")]
public sealed class GetBaseplateTemperatureLimits : QueryPayload<TemperatureLimits>
{
    private protected override Func<Laser, TemperatureLimits> CreateQuery() => laser => laser.GetBaseplateTemperatureLimits();
}

/// <summary>Gets the diode temperature range outside of which the laser faults (OBIS LX).</summary>
[Description("Gets the diode temperature range outside of which the laser faults (OBIS LX).")]
public sealed class GetDiodeTemperatureLimits : QueryPayload<TemperatureLimits>
{
    private protected override Func<Laser, TemperatureLimits> CreateQuery() => laser => laser.GetDiodeTemperatureLimits();
}

/// <summary>Gets the internal temperature range outside of which the laser faults (OBIS LX).</summary>
[Description("Gets the internal temperature range outside of which the laser faults (OBIS LX).")]
public sealed class GetInternalTemperatureLimits : QueryPayload<TemperatureLimits>
{
    private protected override Func<Laser, TemperatureLimits> CreateQuery() => laser => laser.GetInternalTemperatureLimits();
}

/// <summary>Gets the laser diode threshold current, in amperes.</summary>
[Description("Gets the laser diode threshold current, in amperes.")]
public sealed class GetThresholdCurrent : QueryPayload<double>
{
    private protected override Func<Laser, double> CreateQuery() => laser => laser.GetThresholdCurrent();
}

/// <summary>Gets the laser diode upper current limit, in amperes (OBIS LX, firmware 2.x or later).</summary>
[Description("Gets the laser diode upper current limit, in amperes (OBIS LX, firmware 2.x or later).")]
public sealed class GetMaxCurrent : QueryPayload<double>
{
    private protected override Func<Laser, double> CreateQuery() => laser => laser.GetMaxCurrent();
}

/// <summary>Gets the OBIS Remote interlock state: true when closed (emission allowed).</summary>
[Description("Gets the OBIS Remote interlock state: true when closed (emission allowed).")]
public sealed class GetInterlock : QueryPayload<bool>
{
    private protected override Func<Laser, bool> CreateQuery() => laser => laser.GetInterlock();
}

/// <summary>Gets the relative power noise level (constant power mode). Above 30 is considered noisy.</summary>
[Description("Gets the relative power noise level (constant power mode). Above 30 is considered noisy.")]
public sealed class GetNoise : QueryPayload<int>
{
    private protected override Func<Laser, int> CreateQuery() => laser => laser.GetNoise();
}

/// <summary>Gets the number of times the device has been powered on.</summary>
[Description("Gets the number of times the device has been powered on.")]
public sealed class GetPowerCycles : QueryPayload<int>
{
    private protected override Func<Laser, int> CreateQuery() => laser => laser.GetPowerCycles();
}

/// <summary>Gets the number of hours the device has been powered on.</summary>
[Description("Gets the number of hours the device has been powered on.")]
public sealed class GetHours : QueryPayload<double>
{
    private protected override Func<Laser, double> CreateQuery() => laser => laser.GetHours();
}

/// <summary>Gets the number of hours the laser diode has been emitting.</summary>
[Description("Gets the number of hours the laser diode has been emitting.")]
public sealed class GetDiodeHours : QueryPayload<double>
{
    private protected override Func<Laser, double> CreateQuery() => laser => laser.GetDiodeHours();
}

// -------------------- Error queue -----------------------------------------------------------

/// <summary>Reads and removes every record of the device error queue, oldest first.</summary>
[Description("Reads and removes every record of the device error queue, oldest first.")]
public sealed class GetErrors : QueryPayload<IReadOnlyList<ErrorRecord>>
{
    private protected override Func<Laser, IReadOnlyList<ErrorRecord>> CreateQuery() => laser => laser.GetErrors();
}

/// <summary>Clears the device error queue.</summary>
[Description("Clears the device error queue.")]
public sealed class ClearErrors : ActionPayload
{
    private protected override Action<Laser> CreateAction() => laser => laser.ClearErrors();
}

// -------------------- Settings --------------------------------------------------------------

/// <summary>Gets whether the laser starts emitting at power-up.</summary>
[Description("Gets whether the laser starts emitting at power-up.")]
public sealed class GetAutoStart : QueryPayload<bool>
{
    private protected override Func<Laser, bool> CreateQuery() => laser => laser.GetAutoStart();
}

/// <summary>Persistently sets whether the laser starts emitting at power-up.</summary>
[Description("Persistently sets whether the laser starts emitting at power-up.")]
public sealed class SetAutoStart : ActionPayload
{
    [Description("Whether the laser starts emitting at power-up.")]
    public bool Enabled { get; set; }

    private protected override Action<Laser> CreateAction()
    {
        var enabled = Enabled;
        return laser => laser.SetAutoStart(enabled);
    }

    public override string ToString() => $"SetAutoStart({Enabled})";
}

/// <summary>Gets whether the 5 s CDRH emission delay is enabled.</summary>
[Description("Gets whether the 5 s CDRH emission delay is enabled.")]
public sealed class GetCdrhDelay : QueryPayload<bool>
{
    private protected override Func<Laser, bool> CreateQuery() => laser => laser.GetCdrhDelay();
}

/// <summary>Persistently enables or disables the 5 s CDRH emission delay.</summary>
[Description("Persistently enables or disables the 5 s CDRH emission delay.")]
public sealed class SetCdrhDelay : ActionPayload
{
    [Description("Whether to enable the CDRH delay. Disabling it makes the system non-CDRH compliant.")]
    public bool Enabled { get; set; } = true;

    private protected override Action<Laser> CreateAction()
    {
        var enabled = Enabled;
        return laser => laser.SetCdrhDelay(enabled);
    }

    public override string ToString() => $"SetCdrhDelay({Enabled})";
}

/// <summary>Gets whether the TEC (LX) or thermal stabilization (LS) is enabled.</summary>
[Description("Gets whether the TEC (LX) or thermal stabilization (LS) is enabled.")]
public sealed class GetTecEnabled : QueryPayload<bool>
{
    private protected override Func<Laser, bool> CreateQuery() => laser => laser.GetTecEnabled();
}

/// <summary>Enables or disables the TEC (LX) or thermal stabilization (LS).</summary>
[Description("Enables or disables the TEC (LX) or thermal stabilization (LS).")]
public sealed class SetTecEnabled : ActionPayload
{
    [Description("Whether to enable the TEC. Off puts an OBIS LX in sleep mode.")]
    public bool Enabled { get; set; } = true;

    private protected override Action<Laser> CreateAction()
    {
        var enabled = Enabled;
        return laser => laser.SetTecEnabled(enabled);
    }

    public override string ToString() => $"SetTecEnabled({Enabled})";
}

/// <summary>Gets whether emission waits for the diode warm-up (OBIS LX).</summary>
[Description("Gets whether emission waits for the diode warm-up (OBIS LX).")]
public sealed class GetDiodeWarmup : QueryPayload<bool>
{
    private protected override Func<Laser, bool> CreateQuery() => laser => laser.GetDiodeWarmup();
}

/// <summary>Persistently sets whether emission waits for the diode warm-up (OBIS LX).</summary>
[Description("Persistently sets whether emission waits for the diode warm-up (OBIS LX).")]
public sealed class SetDiodeWarmup : ActionPayload
{
    [Description("Whether emission waits for the diode warm-up.")]
    public bool Enabled { get; set; } = true;

    private protected override Action<Laser> CreateAction()
    {
        var enabled = Enabled;
        return laser => laser.SetDiodeWarmup(enabled);
    }

    public override string ToString() => $"SetDiodeWarmup({Enabled})";
}

/// <summary>Gets whether the status LED is enabled.</summary>
[Description("Gets whether the status LED is enabled.")]
public sealed class GetStatusIndicator : QueryPayload<bool>
{
    private protected override Func<Laser, bool> CreateQuery() => laser => laser.GetStatusIndicator();
}

/// <summary>Persistently turns the status LED on or off; status bits are not affected.</summary>
[Description("Persistently turns the status LED on or off; status bits are not affected.")]
public sealed class SetStatusIndicator : ActionPayload
{
    [Description("Whether to turn the status LED on.")]
    public bool Enabled { get; set; } = true;

    private protected override Action<Laser> CreateAction()
    {
        var enabled = Enabled;
        return laser => laser.SetStatusIndicator(enabled);
    }

    public override string ToString() => $"SetStatusIndicator({Enabled})";
}

/// <summary>Gets whether blanking is enabled in analog modulation modes.</summary>
[Description("Gets whether blanking is enabled in analog modulation modes.")]
public sealed class GetAnalogBlanking : QueryPayload<bool>
{
    private protected override Func<Laser, bool> CreateQuery() => laser => laser.GetAnalogBlanking();
}

/// <summary>Enables or disables blanking in analog modulation modes.</summary>
[Description("Enables or disables blanking in analog modulation modes.")]
public sealed class SetAnalogBlanking : ActionPayload
{
    [Description("Whether to enable blanking.")]
    public bool Enabled { get; set; }

    private protected override Action<Laser> CreateAction()
    {
        var enabled = Enabled;
        return laser => laser.SetAnalogBlanking(enabled);
    }

    public override string ToString() => $"SetAnalogBlanking({Enabled})";
}

/// <summary>Gets the OBIS Remote analog input impedance.</summary>
[Description("Gets the OBIS Remote analog input impedance.")]
public sealed class GetAnalogImpedance : QueryPayload<AnalogImpedance>
{
    private protected override Func<Laser, AnalogImpedance> CreateQuery() => laser => laser.GetAnalogImpedance();
}

/// <summary>Persistently selects the OBIS Remote analog input impedance.</summary>
[Description("Persistently selects the OBIS Remote analog input impedance.")]
public sealed class SetAnalogImpedance : ActionPayload
{
    [Description("The analog input impedance.")]
    public AnalogImpedance Impedance { get; set; } = AnalogImpedance.Ohm50;

    private protected override Action<Laser> CreateAction()
    {
        var impedance = Impedance;
        return laser => laser.SetAnalogImpedance(impedance);
    }

    public override string ToString() => $"SetAnalogImpedance({Impedance})";
}

// -------------------- Maintenance -----------------------------------------------------------

/// <summary>Warm-boots the device. Clears fault conditions.</summary>
[Description("Warm-boots the device. Clears fault conditions.")]
public sealed class Reset : ActionPayload
{
    private protected override Action<Laser> CreateAction() => laser => laser.Reset();
}

/// <summary>Runs the self-test. 0xFFFFFFFF means the self-test is not implemented.</summary>
[Description("Runs the self-test. 0xFFFFFFFF means the self-test is not implemented.")]
public sealed class SelfTest : QueryPayload<FaultFlags>
{
    private protected override Func<Laser, FaultFlags> CreateQuery() => laser => laser.SelfTest();
}

/// <summary>Starts a power field calibration (OBIS LX). Takes minutes; do not cut power.</summary>
[Description("Starts a power field calibration (OBIS LX). Takes minutes; do not cut power.")]
public sealed class StartFieldCalibration : ActionPayload
{
    private protected override Action<Laser> CreateAction() => laser => laser.StartFieldCalibration();
}

/// <summary>Undoes the last field calibration (OBIS LX).</summary>
[Description("Undoes the last field calibration (OBIS LX).")]
public sealed class UndoFieldCalibration : ActionPayload
{
    private protected override Action<Laser> CreateAction() => laser => laser.UndoFieldCalibration();
}

/// <summary>Recovers from a persistent checksum error, restoring factory defaults. Ask Coherent before using it.</summary>
[Description("Recovers from a persistent checksum error, restoring factory defaults. Ask Coherent before using it.")]
public sealed class Recover : ActionPayload
{
    private protected override Action<Laser> CreateAction() => laser => laser.Recover();
}

// -------------------- Raw commands ----------------------------------------------------------

/// <summary>Sends any command or query text and returns the reply lines.</summary>
[Description("Sends any command or query text and returns the reply lines.")]
public sealed class Query : QueryPayload<IReadOnlyList<string>>
{
    [Description("The command or query text, e.g. SYST:INF:MOD?")]
    public string Text { get; set; } = "";

    private protected override Func<Laser, IReadOnlyList<string>> CreateQuery()
    {
        var text = Text ?? string.Empty;
        return laser => laser.Query(text);
    }

    public override string ToString() => $"Query({Text})";
}

/// <summary>Sends a command to every laser connected to an OBIS Remote or LaserBox. The channel is ignored.</summary>
[Description("Sends a command to every laser connected to an OBIS Remote or LaserBox. The channel is ignored.")]
public sealed class Broadcast : ActionPayload
{
    [Description("The command text, e.g. SOUR:AM:STAT ON. Queries cannot be broadcast.")]
    public string Text { get; set; } = "";

    private protected override Action<Laser> CreateAction()
    {
        var text = Text ?? string.Empty;
        return laser => laser.Connection.Broadcast(text);
    }

    public override string ToString() => $"Broadcast({Text})";
}

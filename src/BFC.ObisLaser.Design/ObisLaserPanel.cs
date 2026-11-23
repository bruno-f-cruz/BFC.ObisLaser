using System.Globalization;
using System.Numerics;
using Hexa.NET.ImGui;
using ObisLaser.Net;

namespace BFC.ObisLaser.Design;
using ImGui = Hexa.NET.ImGui.ImGui;
using static ObisTheme;

/// <summary>Draws the laser dashboard of an <see cref="ObisLaserVisualizer"/> in a Dear ImGui frame.</summary>
internal sealed class ObisLaserPanel
{
    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    static readonly TimeSpan PendingTimeout = TimeSpan.FromSeconds(3);
    const string NotSupported = "Not supported";
    const string ReadOnlyLabel = "READ-ONLY";

    static readonly (OperatingMode Mode, string Label, string Description)[] Modes =
    {
        (OperatingMode.Cwp, "CWP", "Continuous wave, constant power."),
        (OperatingMode.Cwc, "CWC", "Continuous wave, constant current."),
        (OperatingMode.Digital, "DIGITAL", "External digital modulation, current feedback."),
        (OperatingMode.Analog, "ANALOG", "External analog modulation, power feedback."),
        (OperatingMode.Mixed, "MIXED", "External digital and analog modulation, current feedback."),
        (OperatingMode.Digso, "DIGSO", "External digital modulation, power feedback. Not supported by all models."),
        (OperatingMode.Mixso, "MIXSO", "External digital and analog modulation, power feedback. Not supported by all models."),
    };

    /// <summary>Queries for the device information section, which change rarely.</summary>
    public static CommandPayload[] DetailQueries() => new CommandPayload[]
    {
        new GetHours(), new GetDiodeHours(), new GetPowerCycles(), new GetThresholdCurrent(), new GetMaxCurrent(),
        new GetDiodeTemperature(), new GetDiodeTemperatureSetpoint(), new GetInternalTemperature(),
        new GetBaseplateTemperatureLimits(), new GetDiodeTemperatureLimits(),
    };

    /// <summary>Queries for the persistent settings section.</summary>
    public static CommandPayload[] SettingQueries() => new CommandPayload[]
    {
        new GetAutoStart(), new GetCdrhDelay(), new GetDiodeWarmup(), new GetTecEnabled(),
        new GetStatusIndicator(), new GetAnalogBlanking(), new GetAnalogImpedance(),
    };

    float powerMilliwatts = float.NaN;
    DateTime powerSentTime;
    bool? emissionRequested;
    DateTime emissionRequestTime;
    bool followLog = true;

    /// <summary>The session of the running workflow, or null when it is not running.</summary>
    public ObisLaserSession? Session { get; set; }

    public void Draw()
    {
        var session = Session;
        if (session is null)
        {
            DrawOffline();
            return;
        }

        var state = session.State;
        var info = session.Info;
        var color = info is null ? Accent : WavelengthColor(info.Wavelength);

        DrawHeader(session, state, info, color);
        DrawControls(session, state, info, color);
        DrawReadings(session, state, info, color);
        if (Section("Device information", defaultOpen: true)) DrawDeviceInformation(session, info);
        if (Section("Settings", defaultOpen: false)) DrawSettings(session);
        if (Section("Status and faults", defaultOpen: false)) DrawStatus(session, state);
        if (Section("Messages", defaultOpen: false)) DrawLog(session);
    }

    static void DrawOffline()
    {
        BeginCard("offline");
        PushBold(1.15f);
        ImGui.TextUnformatted("OBIS laser");
        ImGui.PopFont();
        ColoredText(Muted, "Start the workflow to connect to the laser.");
        EndCard();
    }

    static void BeginCard(string id)
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, Card);
        ImGui.BeginChild(id, new Vector2(ImGui.GetContentRegionAvail().X, 0), ImGuiChildFlags.AlwaysUseWindowPadding | ImGuiChildFlags.AutoResizeY, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
    }

    static void EndCard()
    {
        ImGui.EndChild();
        ImGui.PopStyleColor();
    }

    // Header, one row: wavelength swatch, wavelength, model and serial number; status and live controls on the right.
    void DrawHeader(ObisLaserSession session, LaserState? state, DeviceInfo? info, Vector4 color)
    {
        BeginCard("header");
        var right = ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X;
        var height = ImGui.GetFrameHeight();
        var radius = Px(6);
        var origin = ImGui.GetCursorScreenPos();
        var center = origin + new Vector2(radius, height * 0.5f);
        var emitting = state?.Emission == true;
        var draw = ImGui.GetWindowDrawList();
        if (emitting) draw.AddCircleFilled(center, radius * 1.7f, U32(WithAlpha(color, 0.25f)));
        draw.AddCircleFilled(center, radius, U32(info is null ? Track : WithAlpha(color, emitting ? 1f : 0.6f)));
        ImGui.Dummy(new Vector2(radius * 2, height));
        ImGui.SameLine(0, Px(8));

        ImGui.AlignTextToFramePadding();
        PushBold();
        ImGui.TextUnformatted(info is null ? "OBIS laser" : string.Format(Invariant, "{0:0} nm", info.Wavelength));
        ImGui.PopFont();
        ImGui.SameLine(0, Px(10));
        ImGui.AlignTextToFramePadding();
        var channel = session.Channel == 0 ? "direct" : $"channel {session.Channel}";
        ColoredText(Muted, info is null ? $"{channel}  ·  waiting for device information" : $"{info.Model}  ·  S/N {info.SerialNumber}  ·  {channel}");

        // Status badge and live controls, aligned right.
        var (status, statusColor) = Status(session, state, color);
        var spacing = ImGui.GetStyle().ItemSpacing.X;
        var badgeWidth = ImGui.CalcTextSize(status).X + Px(14);
        if (session.ReadOnly) badgeWidth += ImGui.CalcTextSize(ReadOnlyLabel).X + Px(14) + spacing;
        var liveWidth = ImGui.CalcTextSize("Live").X + spacing + height * 1.75f;
        var refreshWidth = ImGui.CalcTextSize("Refresh").X + ImGui.GetStyle().FramePadding.X * 2;
        ImGui.SameLine(Math.Max(ImGui.GetCursorPosX(), right - badgeWidth - liveWidth - refreshWidth - spacing * 3));
        if (session.ReadOnly)
        {
            Badge(ReadOnlyLabel, Muted);
            ImGui.SetItemTooltip("The Disable property of this node is set: the laser can be viewed but not changed.");
            ImGui.SameLine();
        }

        Badge(status, statusColor);
        ImGui.SameLine(0, spacing * 2);
        ImGui.AlignTextToFramePadding();
        ColoredText(Muted, "Live");
        ImGui.SameLine();
        if (Switch("##live", session.Polling, Accent)) session.Polling = !session.Polling;
        ImGui.SetItemTooltip("Request the laser state every poll interval.");
        ImGui.SameLine();
        if (ImGui.Button("Refresh"))
        {
            session.Send(new GetInfo(), new GetState());
            session.Send(DetailQueries());
            session.Send(SettingQueries());
        }

        ImGui.SetItemTooltip("Read the device information, state and settings again.");
        EndCard();
    }

    static (string Text, Vector4 Color) Status(ObisLaserSession session, LaserState? state, Vector4 color)
    {
        if (state is null) return ("WAITING", Muted);
        if (session.Polling && DateTime.Now - session.LastReplyTime > TimeSpan.FromSeconds(5)) return ("NO REPLY", Warning);
        if (state.Faults != FaultFlags.None || state.Status.HasFlag(StatusFlags.LaserFault)) return ("FAULT", Bad);
        if (state.Emission && state.Status.HasFlag(StatusFlags.CdrhDelay)) return ("CDRH DELAY", Warning);
        if (state.Emission && state.Status.HasFlag(StatusFlags.LaserWarmUp)) return ("WARMING UP", Warning);
        if (state.Emission) return ("EMITTING", color);
        return ("STANDBY", Muted);
    }

    // Controls: emission switch, power slider and operating mode.
    void DrawControls(ObisLaserSession session, LaserState? state, DeviceInfo? info, Vector4 color)
    {
        ImGui.Spacing();
        ImGui.BeginDisabled(session.ReadOnly);
        var labelWidth = Px(64);

        // Emission: shows the requested value until the laser reports it, or for a few seconds.
        if (emissionRequested.HasValue && (state?.Emission == emissionRequested || DateTime.Now - emissionRequestTime > PendingTimeout)) emissionRequested = null;
        var emission = emissionRequested ?? state?.Emission;
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Emission");
        ImGui.SameLine(labelWidth);
        if (Switch("##emission", emission, color) && emission.HasValue)
        {
            emissionRequested = !emission.Value;
            emissionRequestTime = DateTime.Now;
            session.Send(new SetEmission { Enabled = emissionRequested.Value }, new GetState());
        }

        ImGui.SetItemTooltip("Turn laser emission on or off. Emission stays on after the workflow stops.");
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        if (emissionRequested.HasValue) ColoredText(Muted, emissionRequested.Value ? "Turning on..." : "Turning off...");
        else if (emission == true) ColoredText(color, "ON");
        else ColoredText(Muted, emission == false ? "OFF" : "Unknown");

        // Power: a slider from 0 to the maximum power, and a box for exact values. Sent on release.
        var maxMilliwatts = (float)((info?.MaxPower ?? 0.1) * 1000);
        var setpoint = state is null ? float.NaN : (float)(state.PowerSetpoint * 1000);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Power");
        ImGui.SameLine(labelWidth);
        var inputWidth = Px(84);
        var sliderWidth = ImGui.GetContentRegionAvail().X - inputWidth - ImGui.GetStyle().ItemSpacing.X;
        var shown = float.IsNaN(powerMilliwatts) ? (float.IsNaN(setpoint) ? 0 : setpoint) : powerMilliwatts;
        if (PowerSlider("##power", ref shown, maxMilliwatts, sliderWidth, color, state is not null)) powerMilliwatts = shown;
        var sliderReleased = ImGui.IsItemDeactivatedAfterEdit();
        var sliderActive = ImGui.IsItemActive();
        ImGui.SetItemTooltip("Drag to set the output power; the power is sent when you release the slider.");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(inputWidth);
        var typed = (double)shown;
        if (ImGui.InputDouble("##powerInput", ref typed, 0, 0, "%.2f mW")) powerMilliwatts = (float)Math.Max(0, typed);
        var typedReleased = ImGui.IsItemDeactivatedAfterEdit();
        var typing = ImGui.IsItemActive();
        if (sliderReleased || typedReleased)
        {
            powerSentTime = DateTime.Now;
            session.Send(new SetPower { Watts = Math.Round(powerMilliwatts, 3) / 1000 }, new GetState());
        }
        else if (!sliderActive && !typing && DateTime.Now - powerSentTime > PendingTimeout)
        {
            powerMilliwatts = float.NaN; // Follow the setpoint reported by the laser.
        }


        // Operating mode.
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Mode");
        ImGui.SameLine(labelWidth);
        for (int i = 0; i < Modes.Length; i++)
        {
            if (i > 0) ImGui.SameLine();
            var (mode, label, description) = Modes[i];
            var selected = state?.OperatingMode == mode;
            if (Pill($"{label}##mode{i}", Accent, selected, state is not null) && !selected) session.Send(new SetOperatingMode { Mode = mode }, new GetState());
            ImGui.SetItemTooltip(description + " Stored in the laser.");
        }

        ImGui.EndDisabled();
        ImGui.Spacing();
    }

    // A slider with a track filled up to the value in the color of the laser.
    static bool PowerSlider(string id, ref float milliwatts, float maxMilliwatts, float width, Vector4 color, bool enabled)
    {
        var position = ImGui.GetCursorScreenPos();
        var height = ImGui.GetFrameHeight();
        var draw = ImGui.GetWindowDrawList();
        var rounding = ImGui.GetStyle().FrameRounding;
        draw.AddRectFilled(position, position + new Vector2(width, height), U32(Track), rounding);
        var fraction = maxMilliwatts > 0 ? Math.Max(0, Math.Min(1, milliwatts / maxMilliwatts)) : 0;
        if (fraction > 0) draw.AddRectFilled(position, position + new Vector2(width * fraction, height), U32(WithAlpha(color, enabled ? 0.45f : 0.2f)), rounding);

        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0f, 0f, 0f, 0f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(1f, 1f, 1f, 0.04f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(1f, 1f, 1f, 0.06f));
        ImGui.PushStyleColor(ImGuiCol.SliderGrab, color);
        ImGui.PushStyleColor(ImGuiCol.SliderGrabActive, color);
        ImGui.SetNextItemWidth(width);
        ImGui.BeginDisabled(!enabled);
        var changed = ImGui.SliderFloat(id, ref milliwatts, 0, maxMilliwatts, "%.2f mW", ImGuiSliderFlags.AlwaysClamp);
        ImGui.EndDisabled();
        ImGui.PopStyleColor(5);
        return changed;
    }

    // Readings: live values as tiles, as many per row as fit.
    // Readings: one small table spanning the window, a label row and a value row.
    static void DrawReadings(ObisLaserSession session, LaserState? state, DeviceInfo? info, Vector4 color)
    {
        var limits = session.GetValue<TemperatureLimits>(nameof(GetBaseplateTemperatureLimits));
        var readings = new (string Label, string Value, Vector4 Color, string? Detail)[]
        {
            ("Output power", state is null ? "-" : FormatMilliwatts(state.Power), state?.Emission == true ? color : Text,
                state is null || state.PowerSetpoint <= 0 || !state.Emission ? null : string.Format(Invariant, "{0:0}% of set", 100 * state.Power / state.PowerSetpoint)),
            ("Setpoint", state is null ? "-" : FormatMilliwatts(state.PowerSetpoint), Text,
                info is null || state is null ? null : string.Format(Invariant, "{0:0.#}% of max", 100 * state.PowerSetpoint / info.MaxPower)),
            ("Diode current", state is null ? "-" : string.Format(Invariant, "{0:0.0} mA", state.Current * 1000), Text, null),
            ("Base plate", state is null ? "-" : string.Format(Invariant, "{0:0.0} °C", state.BaseplateTemperature),
                TemperatureColor(state?.BaseplateTemperature, limits),
                limits is { } range ? string.Format(Invariant, "max {0:0} °C", range.High) : null),
            ("Hours", session.GetValue<double>(nameof(GetHours)) is { } hours ? hours.ToString("N0", Invariant) + " h" : "-", Text,
                session.GetValue<double>(nameof(GetDiodeHours)) is { } diodeHours ? diodeHours.ToString("N0", Invariant) + " h diode" : null),
        };

        const ImGuiTableFlags Flags = ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.BordersOuterH | ImGuiTableFlags.SizingStretchSame;
        if (!ImGui.BeginTable("##readings", readings.Length, Flags)) return;
        ImGui.TableNextRow();
        for (int i = 0; i < readings.Length; i++)
        {
            ImGui.TableSetColumnIndex(i);
            ColoredText(Muted, readings[i].Label);
        }

        ImGui.TableNextRow();
        for (int i = 0; i < readings.Length; i++)
        {
            ImGui.TableSetColumnIndex(i);
            PushBold();
            ColoredText(readings[i].Color, readings[i].Value);
            ImGui.PopFont();
            if (readings[i].Detail is { } detail)
            {
                ImGui.SameLine();
                ColoredText(Muted, detail);
            }
        }

        ImGui.EndTable();
    }

    static Vector4 TemperatureColor(double? temperature, TemperatureLimits? limits)
    {
        if (temperature is not { } value || limits is not { } range) return Text;
        if (value <= range.Low || value >= range.High) return Bad;
        return value >= range.High - 3 || value <= range.Low + 3 ? Warning : Good;
    }

    // Device information: metadata and slowly changing readings.
    static void DrawDeviceInformation(ObisLaserSession session, DeviceInfo? info)
    {
        if (info is null)
        {
            ColoredText(Muted, "Waiting for device information.");
            return;
        }

        string Missing(string command) => session.IsUnsupported(command) ? NotSupported : "-";
        string Temperature(string command) => session.GetValue<double>(command) is { } value ? string.Format(Invariant, "{0:0.0} °C", value) : Missing(command);
        string Limits(string command) => session.GetValue<TemperatureLimits>(command) is { } value ? string.Format(Invariant, "{0:0.#} to {1:0.#} °C", value.Low, value.High) : Missing(command);
        string Current(string command) => session.GetValue<double>(command) is { } value ? string.Format(Invariant, "{0:0.0} mA", value * 1000) : Missing(command);
        string Hours(string command) => session.GetValue<double>(command) is { } value ? value.ToString("N1", Invariant) + " h" : Missing(command);

        var rows = new (string Name, string Value)[]
        {
            ("Model", info.Model),
            ("Wavelength", string.Format(Invariant, "{0:0.#} nm", info.Wavelength)),
            ("Serial number", info.SerialNumber),
            ("Part number", info.PartNumber),
            ("Type", info.DeviceType.ToString().ToUpperInvariant()),
            ("Firmware", info.FirmwareVersion),
            ("Protocol", info.ProtocolVersion),
            ("Power rating", FormatMilliwatts(info.PowerRating)),
            ("Nominal power", FormatMilliwatts(info.NominalPower)),
            ("Power range", $"{FormatMilliwatts(info.MinPower)} to {FormatMilliwatts(info.MaxPower)}"),
            ("Manufactured", FormatDate(info.ManufactureDate)),
            ("Calibrated", FormatDate(info.CalibrationDate)),
            ("Operating hours", Hours(nameof(GetHours))),
            ("Diode hours", Hours(nameof(GetDiodeHours))),
            ("Power cycles", session.GetValue<int>(nameof(GetPowerCycles))?.ToString("N0", Invariant) ?? Missing(nameof(GetPowerCycles))),
            ("Threshold current", Current(nameof(GetThresholdCurrent))),
            ("Maximum current", Current(nameof(GetMaxCurrent))),
            ("Diode temperature", Temperature(nameof(GetDiodeTemperature)) + (session.GetValue<double>(nameof(GetDiodeTemperatureSetpoint)) is { } set ? string.Format(Invariant, " (set {0:0.0} °C)", set) : string.Empty)),
            ("Internal temperature", Temperature(nameof(GetInternalTemperature))),
            ("Base plate limits", Limits(nameof(GetBaseplateTemperatureLimits))),
            ("Diode limits", Limits(nameof(GetDiodeTemperatureLimits))),
        };

        var pairs = ImGui.GetContentRegionAvail().X > Px(480) ? 2 : 1;
        const ImGuiTableFlags Flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp;
        if (ImGui.BeginTable("##info", pairs * 2, Flags))
        {
            for (int pair = 0; pair < pairs; pair++)
            {
                ImGui.TableSetupColumn($"Name{pair}", ImGuiTableColumnFlags.WidthStretch, 0.7f);
                ImGui.TableSetupColumn($"Value{pair}", ImGuiTableColumnFlags.WidthStretch, 1f);
            }

            var perColumn = (rows.Length + pairs - 1) / pairs;
            for (int row = 0; row < perColumn; row++)
            {
                ImGui.TableNextRow();
                for (int pair = 0; pair < pairs; pair++)
                {
                    var index = pair * perColumn + row;
                    if (index >= rows.Length) continue;
                    ImGui.TableSetColumnIndex(pair * 2);
                    ColoredText(Muted, rows[index].Name);
                    ImGui.TableSetColumnIndex(pair * 2 + 1);
                    var value = rows[index].Value;
                    if (value == NotSupported || value == "-") ColoredText(Muted, value);
                    else ImGui.TextUnformatted(value);
                }
            }

            ImGui.EndTable();
        }

        if (ImGui.Button("Read again##details"))
        {
            session.Send(new GetInfo());
            session.Send(DetailQueries());
        }

        ImGui.SetItemTooltip("Read the device information, hours and temperatures again.");
    }

    // Settings: persistent configuration of the laser.
    static void DrawSettings(ObisLaserSession session)
    {
        ColoredText(Warning, session.ReadOnly ? "These settings are stored in the laser. Read-only: they cannot be changed here." : "These settings are stored in the laser and persist after power cycles.");
        var labelWidth = Px(110);
        bool? Setting(string command) => session.GetValue<bool>(command);

        void Row(string label, string get, string tooltip, Func<bool, CommandPayload> set, CommandPayload query)
        {
            if (session.IsUnsupported(get)) return;
            var value = Setting(get);
            if (SwitchRow(label, $"##{get}", value, tooltip, labelWidth) && value.HasValue) session.Send(set(!value.Value), query);
        }

        ImGui.BeginDisabled(session.ReadOnly);
        Row("Auto start", nameof(GetAutoStart), "Turn emission on when the laser is powered.", on => new SetAutoStart { Enabled = on }, new GetAutoStart());
        Row("CDRH delay", nameof(GetCdrhDelay), "Wait 5 s before emission starts, as required by CDRH.", on => new SetCdrhDelay { Enabled = on }, new GetCdrhDelay());
        Row("Diode warm-up", nameof(GetDiodeWarmup), "Warm up the laser diode before emission.", on => new SetDiodeWarmup { Enabled = on }, new GetDiodeWarmup());
        Row("TEC", nameof(GetTecEnabled), "Thermoelectric cooler of the diode (LX lasers).", on => new SetTecEnabled { Enabled = on }, new GetTecEnabled());
        Row("Status indicator", nameof(GetStatusIndicator), "The status LED on the laser.", on => new SetStatusIndicator { Enabled = on }, new GetStatusIndicator());
        Row("Analog blanking", nameof(GetAnalogBlanking), "Blank emission when the analog modulation input is low.", on => new SetAnalogBlanking { Enabled = on }, new GetAnalogBlanking());

        if (!session.IsUnsupported(nameof(GetAnalogImpedance))) DrawImpedance(session, labelWidth);
        ImGui.EndDisabled();
        if (ImGui.Button("Read again##settings")) session.Send(SettingQueries());
    }

    static void DrawImpedance(ObisLaserSession session, float labelWidth)
    {
        var impedance = session.GetValue<AnalogImpedance>(nameof(GetAnalogImpedance));
        var start = ImGui.GetCursorPosX();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Analog input");
        ImGui.SetItemTooltip("Input impedance of the analog modulation input.");
        ImGui.SameLine(start + labelWidth);
        if (Pill("50 Ω##impedance50", Accent, impedance == AnalogImpedance.Ohm50, impedance.HasValue) && impedance != AnalogImpedance.Ohm50)
            session.Send(new SetAnalogImpedance { Impedance = AnalogImpedance.Ohm50 }, new GetAnalogImpedance());
        ImGui.SameLine();
        if (Pill("2 kΩ##impedance2k", Accent, impedance == AnalogImpedance.Kohm2, impedance.HasValue) && impedance != AnalogImpedance.Kohm2)
            session.Send(new SetAnalogImpedance { Impedance = AnalogImpedance.Kohm2 }, new GetAnalogImpedance());
    }

    // Status and faults: flags of the latest state, the error queue and recovery.
    static void DrawStatus(ObisLaserSession session, LaserState? state)
    {
        if (state is null)
        {
            ColoredText(Muted, "Waiting for the laser state.");
            return;
        }

        ColoredText(Muted, "Status");
        var first = true;
        foreach (StatusFlags flag in Enum.GetValues(typeof(StatusFlags)))
        {
            if (flag == 0 || !state.Status.HasFlag(flag)) continue;
            if (!first) ImGui.SameLine();
            Badge(Words(flag.ToString()), flag is StatusFlags.LaserFault or StatusFlags.LaserHardwareFault ? Bad : flag == StatusFlags.LaserEmission ? Good : Muted);
            first = false;
        }

        ColoredText(Muted, "Faults");
        if (state.Faults == FaultFlags.None)
        {
            Badge("No faults", Good);
        }
        else
        {
            first = true;
            foreach (FaultFlags flag in Enum.GetValues(typeof(FaultFlags)))
            {
                if (flag == FaultFlags.None || !state.Faults.HasFlag(flag)) continue;
                if (!first) ImGui.SameLine();
                Badge(Words(flag.ToString()), Bad);
                first = false;
            }

            ImGui.BeginDisabled(session.ReadOnly);
            if (ColoredButton("Recover", Danger)) session.Send(new Recover(), new GetState());
            ImGui.EndDisabled();
            ImGui.SetItemTooltip("Try to recover the laser from a fault.");
        }

        ColoredText(Muted, "Error queue");
        var errors = session.Get<IReadOnlyList<ErrorRecord>>(nameof(GetErrors));
        if (errors is null) ColoredText(Muted, "Not read yet.");
        else if (errors.Count == 0) Badge("Empty", Good);
        else foreach (var error in errors) ColoredText(Warning, $"{error.Code}  {error.Message}");
        if (ImGui.Button("Read##errors")) session.Send(new GetErrors());
        ImGui.SameLine();
        ImGui.BeginDisabled(session.ReadOnly);
        if (ImGui.Button("Clear##errors")) session.Send(new ClearErrors(), new GetErrors(), new GetState());
        ImGui.EndDisabled();
        ImGui.SetItemTooltip("Clear the error queue of the laser.");
    }

    // Messages: every reply to the commands of this channel, newest last.
    void DrawLog(ObisLaserSession session)
    {
        var log = session.GetLog();
        ColoredText(Muted, $"{log.Length} replies");
        ImGui.SameLine();
        if (ImGui.SmallButton("Clear##log")) session.ClearLog();
        ImGui.SameLine();
        ImGui.Checkbox("Follow", ref followLog);

        const ImGuiTableFlags Flags = ImGuiTableFlags.ScrollY | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.Resizable | ImGuiTableFlags.SizingStretchProp;
        if (!ImGui.BeginTable("##log", 4, Flags, new Vector2(0, Px(180)))) return;
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableSetupColumn("Command", ImGuiTableColumnFlags.WidthStretch, 1);
        ImGui.TableSetupColumn("Reply", ImGuiTableColumnFlags.WidthStretch, 2);
        ImGui.TableSetupColumn("ms", ImGuiTableColumnFlags.WidthFixed);
        ImGui.TableHeadersRow();
        foreach (var entry in log)
        {
            ImGui.TableNextRow();
            ImGui.TableSetColumnIndex(0);
            ColoredText(Muted, entry.Time.ToString("HH:mm:ss.fff", Invariant));
            ImGui.TableSetColumnIndex(1);
            ImGui.TextUnformatted(entry.Command);
            ImGui.TableSetColumnIndex(2);
            if (entry.IsSuccess) ImGui.TextUnformatted(entry.Value ?? string.Empty);
            else ColoredText(Bad, entry.Error ?? string.Empty);
            if (ImGui.IsItemHovered()) ImGui.SetTooltip((entry.IsSuccess ? entry.Value ?? string.Empty : entry.Error ?? string.Empty).Replace("%", "%%"));
            ImGui.TableSetColumnIndex(3);
            if (entry.Duration is { } duration) ColoredText(Muted, duration.TotalMilliseconds.ToString("F1", Invariant));
        }

        if (followLog && ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 1) ImGui.SetScrollHereY(1);
        ImGui.EndTable();
    }

    static string FormatMilliwatts(double watts) => (watts * 1000).ToString(watts < 0.01 ? "0.00" : "0.0", Invariant) + " mW";

    // Dates are reported as yyyymmdd.
    static string FormatDate(string text) =>
        DateTime.TryParseExact(text, "yyyyMMdd", Invariant, DateTimeStyles.None, out var date) ? date.ToString("yyyy-MM-dd", Invariant) : text;

    // "LaserPowerVoltage" -> "Laser power voltage".
    static string Words(string name)
    {
        var builder = new System.Text.StringBuilder(name.Length + 8);
        for (int i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (i > 0 && char.IsUpper(c) && !char.IsUpper(name[i - 1])) builder.Append(' ').Append(char.ToLowerInvariant(c));
            else builder.Append(c);
        }

        return builder.ToString();
    }
}

using System.Globalization;
using System.Numerics;
using Hexa.NET.ImGui;
using ObisLaser.Net;

namespace BFC.ObisLaser.Design;
using ImGui = Hexa.NET.ImGui.ImGui;

/// <summary>Draws the control panel of an <see cref="ObisLaserVisualizer"/> in a Dear ImGui frame.</summary>
internal sealed class ObisLaserPanel
{
    static readonly Vector4 Green = new(0.35f, 0.85f, 0.45f, 1f);
    static readonly Vector4 Red = new(1f, 0.45f, 0.45f, 1f);
    static readonly Vector4 Amber = new(1f, 0.75f, 0.3f, 1f);
    static readonly Vector4 Dim = new(0.6f, 0.6f, 0.6f, 1f);
    static readonly OperatingMode[] Modes = (OperatingMode[])Enum.GetValues(typeof(OperatingMode));
    static readonly string[] ModeNames = Modes.Select(mode => mode.ToString().ToUpperInvariant()).ToArray();

    double powerMilliwatts = double.NaN;
    int modeIndex = -1;
    bool followLog = true;

    /// <summary>The session of the running workflow, or null when it is not running.</summary>
    public ObisLaserSession? Session { get; set; }

    public void Draw()
    {
        var session = Session;
        if (session is null)
        {
            ImGui.TextUnformatted("Start the workflow to control the laser.");
            return;
        }

        var state = session.State;
        var info = session.Info;
        DrawHeader(session, info);
        ImGui.Separator();
        DrawState(state);
        ImGui.Separator();
        DrawControls(session, state, info);
        ImGui.Spacing();
        DrawLog(session);
    }

    static void DrawHeader(ObisLaserSession session, DeviceInfo? info)
    {
        var channel = session.Channel == 0 ? string.Empty : $"  ·  channel {session.Channel}";
        if (info is null)
        {
            ImGui.TextUnformatted($"OBIS laser{channel}");
            ImGui.SameLine();
            ImGui.TextColored(Dim, "(waiting for device information)");
        }
        else
        {
            ImGui.TextUnformatted($"{info.Model}  ·  S/N {info.SerialNumber}{channel}");
            ImGui.TextColored(Dim, $"{info.Wavelength:F0} nm  ·  {Milliwatts(info.MinPower)}–{Milliwatts(info.MaxPower)}  ·  firmware {info.FirmwareVersion}");
        }
    }

    static void DrawState(LaserState? state)
    {
        if (state is null)
        {
            ImGui.TextColored(Dim, "No state yet: press Refresh or turn on Poll state.");
            return;
        }

        ImGui.TextUnformatted("Emission");
        ImGui.SameLine();
        if (state.Emission) ImGui.TextColored(Green, "ON");
        else ImGui.TextColored(Dim, "OFF");
        ImGui.SameLine();
        ImGui.TextUnformatted($"   Power {Milliwatts(state.Power)} (set {Milliwatts(state.PowerSetpoint)})");

        ImGui.TextUnformatted(string.Format(CultureInfo.InvariantCulture,
            "Mode {0}   Current {1:F1} mA   Base plate {2:F1} °C",
            state.OperatingMode.ToString().ToUpperInvariant(), state.Current * 1000, state.BaseplateTemperature));

        ImGui.TextUnformatted($"Status {state.Status}");
        if (state.Faults != FaultFlags.None) ImGui.TextColored(Red, $"Faults {state.Faults}");
    }

    void DrawControls(ObisLaserSession session, LaserState? state, DeviceInfo? info)
    {
        if (ImGui.Button("Emission ON")) SendAndRefresh(session, new SetEmission { Enabled = true });
        ImGui.SameLine();
        if (ImGui.Button("Emission OFF")) SendAndRefresh(session, new SetEmission { Enabled = false });

        if (double.IsNaN(powerMilliwatts) && state is not null) powerMilliwatts = state.PowerSetpoint * 1000;
        var power = double.IsNaN(powerMilliwatts) ? 0 : powerMilliwatts;
        ImGui.SetNextItemWidth(ImGui.GetFontSize() * 10);
        if (ImGui.InputDouble("mW##power", ref power, 0.1, 1, "%.2f")) powerMilliwatts = Math.Max(0, power);
        ImGui.SameLine();
        if (ImGui.Button("Set power")) SendAndRefresh(session, new SetPower { Watts = powerMilliwatts / 1000 });
        if (info is not null && powerMilliwatts > info.MaxPower * 1000)
        {
            ImGui.SameLine();
            ImGui.TextColored(Amber, $"above maximum ({Milliwatts(info.MaxPower)})");
        }

        if (modeIndex < 0 && state is not null) modeIndex = Array.IndexOf(Modes, state.OperatingMode);
        var mode = Math.Max(0, modeIndex);
        ImGui.SetNextItemWidth(ImGui.GetFontSize() * 10);
        if (ImGui.Combo("##mode", ref mode, ModeNames, ModeNames.Length)) modeIndex = mode;
        ImGui.SameLine();
        if (ImGui.Button("Set mode")) SendAndRefresh(session, new SetOperatingMode { Mode = Modes[mode] });

        if (ImGui.Button("Refresh"))
        {
            session.Send(new GetInfo());
            session.Send(new GetState());
        }

        ImGui.SameLine();
        var polling = session.Polling;
        if (ImGui.Checkbox("Poll state", ref polling)) session.Polling = polling;
    }

    void DrawLog(ObisLaserSession session)
    {
        var log = session.GetLog();
        if (!ImGui.CollapsingHeader($"Messages ({log.Length})###messages")) return;

        if (ImGui.Button("Clear")) session.ClearLog();
        ImGui.SameLine();
        ImGui.Checkbox("Follow", ref followLog);

        const ImGuiTableFlags Flags = ImGuiTableFlags.ScrollY | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.Resizable;
        if (!ImGui.BeginTable("log", 4, Flags, new Vector2(0, 0))) return;
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
            ImGui.TextColored(Dim, entry.Time.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture));
            ImGui.TableSetColumnIndex(1);
            ImGui.TextUnformatted(entry.Command);
            ImGui.TableSetColumnIndex(2);
            if (entry.IsSuccess) ImGui.TextUnformatted(entry.Value ?? string.Empty);
            else ImGui.TextColored(Red, entry.Error ?? string.Empty);
            ImGui.TableSetColumnIndex(3);
            if (entry.Duration is { } duration) ImGui.TextColored(Dim, duration.TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture));
        }

        if (followLog && ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 1) ImGui.SetScrollHereY(1);
        ImGui.EndTable();
    }

    static void SendAndRefresh(ObisLaserSession session, CommandPayload payload)
    {
        if (session.Send(payload)) session.Send(new GetState());
    }

    static string Milliwatts(double watts) => (watts * 1000).ToString("0.##", CultureInfo.InvariantCulture) + " mW";
}

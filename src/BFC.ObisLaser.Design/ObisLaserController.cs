using System.Numerics;
using System.Reactive.Linq;
using Hexa.NET.ImGui;

namespace BFC.ObisLaser.Design;
using ImGui = Hexa.NET.ImGui.ImGui;

/// <summary>
/// The laser dashboard of one OBIS laser, to draw inside an ImGui window of your own, for example one tab
/// per laser in a Bonsai.ImGui visualizer.
/// </summary>
/// <remarks>
/// Send <see cref="Commands"/> to the <see cref="Device"/> of the laser, pass its replies to
/// <see cref="Receive"/>, and call <see cref="Draw"/> on each frame, from the thread that renders the
/// ImGui context. The first call to <see cref="Draw"/> requests the device information, state and
/// settings. Dispose the controller to stop polling the laser.
/// </remarks>
public sealed class ObisLaserController : IDisposable
{
    readonly ObisLaserSession session;
    readonly ObisLaserPanel panel = new();
    readonly IDisposable polling;
    bool requestedInfo;

    /// <summary>Creates the dashboard of the laser on a channel, polling its state every second.</summary>
    /// <param name="channel">The laser channel: 0 for a laser connected directly, 1 or more behind an OBIS Remote or LaserBox.</param>
    public ObisLaserController(int channel)
        : this(channel, TimeSpan.FromSeconds(1))
    {
    }

    /// <summary>Creates the dashboard of the laser on a channel.</summary>
    /// <param name="channel">The laser channel: 0 for a laser connected directly, 1 or more behind an OBIS Remote or LaserBox.</param>
    /// <param name="pollInterval">The interval between state requests while <see cref="PollState"/> is set.</param>
    public ObisLaserController(int channel, TimeSpan pollInterval)
    {
        if (pollInterval <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(pollInterval), "The poll interval must be positive.");
        session = new ObisLaserSession(channel, () => Disable) { Polling = true };
        polling = Observable.Interval(pollInterval).Subscribe(_ => session.Poll());
        panel.Session = session;
    }

    /// <summary>The channel of the laser.</summary>
    public int Channel => session.Channel;

    /// <summary>
    /// Whether the dashboard is read-only: it shows the laser and requests its state, but cannot change emission,
    /// power, mode or settings.
    /// </summary>
    public bool Disable { get; set; }

    /// <summary>Whether the laser state is requested periodically. Can be toggled from the dashboard.</summary>
    public bool PollState
    {
        get => session.Polling;
        set => session.Polling = value;
    }

    /// <summary>The commands requested from the dashboard, for the <see cref="Device"/> of the laser.</summary>
    public IObservable<ObisCommand> Commands => session.Commands;

    /// <summary>The model the laser reports, once its device information has arrived.</summary>
    public string? Model => session.Info?.Model;

    /// <summary>The nominal wavelength the laser reports (nm), once its device information has arrived.</summary>
    public double? Wavelength => session.Info?.Wavelength;

    /// <summary>Whether emission is on, once the laser state has arrived.</summary>
    public bool? IsEmitting => session.State?.Emission;

    /// <summary>Shows a reply of the laser. Replies to commands for other channels are ignored.</summary>
    public void Receive(ObisReply reply)
    {
        if (reply is null) throw new ArgumentNullException(nameof(reply));
        if (reply.Command.Channel == Channel) session.Receive(reply);
    }

    /// <summary>
    /// Draws the dashboard in the current ImGui window, filling the space available, with its own scroll bar.
    /// Does not change the style or colors of the ImGui context.
    /// </summary>
    public void Draw()
    {
        if (!requestedInfo)
        {
            requestedInfo = true;
            session.Send(new GetInfo(), new GetState());
            session.Send(ObisLaserPanel.DetailQueries());
            session.Send(ObisLaserPanel.SettingQueries());
        }

        ObisTheme.ApplyEmbedded();
        ImGui.PushID(GetHashCode());
        ImGui.BeginChild("##ObisLaserController", Vector2.Zero, ImGuiChildFlags.None, ImGuiWindowFlags.None);
        try { panel.Draw(); }
        finally
        {
            ImGui.EndChild();
            ImGui.PopID();
        }
    }

    /// <summary>Stops polling the laser.</summary>
    public void Dispose() => polling.Dispose();
}

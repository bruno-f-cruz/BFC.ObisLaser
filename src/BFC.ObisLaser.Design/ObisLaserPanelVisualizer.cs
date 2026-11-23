using System.Numerics;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Bonsai.Design;
using Bonsai.Expressions;
using Bonsai.ImGui.Design;
using Hexa.NET.ImGui;
using Timer = System.Windows.Forms.Timer;

[assembly: Bonsai.TypeVisualizer(typeof(BFC.ObisLaser.Design.ObisLaserPanelVisualizer), Target = typeof(BFC.ObisLaser.ObisLaserVisualizer))]

namespace BFC.ObisLaser.Design;

/// <summary>
/// Shows the control panel of an <see cref="ObisLaserVisualizer"/> node: the laser state, controls
/// that send commands, and a collapsible log of the replies.
/// </summary>
public sealed class ObisLaserPanelVisualizer : DialogTypeVisualizer
{
    readonly ObisLaserPanel panel = new();
    ObisLaserVisualizer? node;
    ObisLaserSession? requestedInfo;
    ImGuiControl? control;
    Timer? timer;
    bool styled;

    public override void Load(IServiceProvider provider)
    {
        var service = provider.GetService(typeof(IDialogTypeVisualizerService)) as IDialogTypeVisualizerService
            ?? throw new InvalidOperationException("The Bonsai visualizer service is required.");
        var context = provider.GetService(typeof(ITypeVisualizerContext)) as ITypeVisualizerContext
            ?? throw new InvalidOperationException("The Bonsai visualizer context is required.");
        node = ExpressionBuilder.GetWorkflowElement(context.Source.Builder) as ObisLaserVisualizer
            ?? throw new InvalidOperationException("This visualizer only supports the ObisLaserVisualizer node.");

        control = new ImGuiControl { Dock = DockStyle.Fill };
        control.Render += (_, _) => Render();
        service.AddControl(control);
        timer = new Timer { Interval = 16 };
        timer.Tick += (_, _) => control?.Invalidate();
        timer.Start();
    }

    void Render()
    {
        var scale = control is null ? 1f : DisplayScale(control);
        ObisTheme.Apply(scale, ref styled);
        ImGui.SetNextWindowPos(Vector2.Zero);
        ImGui.SetNextWindowSize(ImGui.GetIO().DisplaySize);
        const ImGuiWindowFlags Flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBringToFrontOnFocus;
        ImGui.Begin("OBIS laser", Flags);
        try { Draw(); }
        finally { ImGui.End(); }
    }

    void Draw()
    {
        var session = node?.Session;
        if (session is not null && !ReferenceEquals(session, requestedInfo))
        {
            // Show what the laser is as soon as the panel connects to a running workflow.
            requestedInfo = session;
            session.Send(new GetInfo(), new GetState());
            session.Send(ObisLaserPanel.DetailQueries());
            session.Send(ObisLaserPanel.SettingQueries());
        }

        panel.Session = session;
        panel.AwaitingChannel = node?.AwaitingChannel ?? false;
        panel.Draw();
    }

    [DllImport("user32.dll")]
    static extern uint GetDpiForWindow(IntPtr hwnd);

    // The DPI of the window, which .NET Framework controls report as 96 unless the application is per-monitor aware.
    static float DisplayScale(Control control)
    {
        uint dpi = 0;
        try
        {
            dpi = GetDpiForWindow(control.Handle);
        }
        catch (EntryPointNotFoundException)
        {
            // Windows versions before 10 (1607).
        }

        return Math.Max(1f, (dpi > 0 ? dpi : control.DeviceDpi) / 96f);
    }

    // The node outputs the commands requested from this panel; the panel shows replies instead.
    public override void Show(object value)
    {
    }

    public override void Unload()
    {
        timer?.Stop();
        timer?.Dispose();
        timer = null;
        control?.Dispose();
        control = null;
        panel.Session = null;
        requestedInfo = null;
        node = null;
    }
}

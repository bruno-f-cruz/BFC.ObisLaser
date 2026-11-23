using System.Windows.Forms;
using Timer = System.Windows.Forms.Timer;
using Bonsai.Design;
using Bonsai.Expressions;
using Bonsai.ImGui.Design;

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

    public override void Load(IServiceProvider provider)
    {
        var service = provider.GetService(typeof(IDialogTypeVisualizerService)) as IDialogTypeVisualizerService
            ?? throw new InvalidOperationException("The Bonsai visualizer service is required.");
        var context = provider.GetService(typeof(ITypeVisualizerContext)) as ITypeVisualizerContext
            ?? throw new InvalidOperationException("The Bonsai visualizer context is required.");
        node = ExpressionBuilder.GetWorkflowElement(context.Source.Builder) as ObisLaserVisualizer
            ?? throw new InvalidOperationException("This visualizer only supports the ObisLaserVisualizer node.");

        control = new ImGuiControl { Dock = DockStyle.Fill };
        control.Render += (_, _) => ImGuiPanel.Draw("OBIS Laser", Draw);
        service.AddControl(control);
        timer = new Timer { Interval = 33 };
        timer.Tick += (_, _) => control?.Invalidate();
        timer.Start();
    }

    void Draw()
    {
        var session = node?.Session;
        if (session is not null && !ReferenceEquals(session, requestedInfo))
        {
            // Show what the laser is as soon as the panel connects to a running workflow.
            requestedInfo = session;
            session.Send(new GetInfo());
            session.Send(new GetState());
        }

        panel.Session = session;
        panel.Draw();
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

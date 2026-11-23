using System.ComponentModel;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Xml;
using System.Xml.Serialization;
using Bonsai;

namespace BFC.ObisLaser;

/// <summary>
/// Shows a control panel for one OBIS laser: displays the replies it receives, and emits the
/// commands requested from the panel.
/// </summary>
/// <remarks>
/// Open the visualizer of this node to see the panel; it requires the BFC.ObisLaser.Design package.
/// Connect the replies of a <see cref="Device"/> to this node, and send its commands back to the
/// same <see cref="Device"/>, typically through subjects: subscribe to the replies subject, and
/// multicast the output to the commands subject. Only replies to commands for <see cref="Channel"/>
/// are shown, and commands are sent to <see cref="Channel"/>.
/// </remarks>
[Combinator]
[WorkflowElementCategory(ElementCategory.Combinator)]
[Description("Shows a control panel for an OBIS laser: displays its replies and emits the commands requested from the panel.")]
public sealed class ObisLaserVisualizer
{
    ObisLaserSession? session;

    [Description("The laser channel: 0 for a laser connected directly, 1 or more for lasers connected to an OBIS Remote or LaserBox.")]
    public int Channel { get; set; }

    [Description("Whether the panel requests the laser state every PollInterval when the workflow starts. Can be toggled in the panel.")]
    public bool PollState { get; set; }

    [Description("Whether the panel is read-only: it shows the laser and can request its state, but cannot change emission, power, mode or settings. Can be changed while the workflow runs.")]
    public bool Disable { get; set; }

    [XmlIgnore]
    [Description("The interval between state requests while the panel polls the laser state.")]
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);

    [Browsable(false)]
    [XmlElement(nameof(PollInterval))]
    [Description("The XML duration used to serialize PollInterval.")]
    public string PollIntervalXml
    {
        get => XmlConvert.ToString(PollInterval);
        set => PollInterval = XmlConvert.ToTimeSpan(value);
    }

    /// <summary>The session of the running workflow, or null when the workflow is not running.</summary>
    internal ObisLaserSession? Session => Volatile.Read(ref session);

    /// <summary>Raised when a session starts or ends.</summary>
    internal event EventHandler? SessionChanged;

    public IObservable<ObisCommand> Process(IObservable<ObisReply> source) => Observable.Create<ObisCommand>(observer =>
    {
        if (PollInterval <= TimeSpan.Zero) throw new InvalidOperationException("PollInterval must be positive.");

        var current = new ObisLaserSession(Channel, () => Disable) { Polling = PollState };
        var channel = Channel;
        var subscriptions = new CompositeDisposable
        {
            current.Commands.Subscribe(observer),
            Observable.Interval(PollInterval).Subscribe(_ => current.Poll()),
            source
                .Where(reply => reply.Command.Channel == channel)
                .Subscribe(current.Receive, observer.OnError, observer.OnCompleted),
        };

        SetSession(current);
        return Disposable.Create(() =>
        {
            subscriptions.Dispose();
            Interlocked.CompareExchange(ref session, null, current);
            SessionChanged?.Invoke(this, EventArgs.Empty);
        });
    });

    void SetSession(ObisLaserSession value)
    {
        Volatile.Write(ref session, value);
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }
}

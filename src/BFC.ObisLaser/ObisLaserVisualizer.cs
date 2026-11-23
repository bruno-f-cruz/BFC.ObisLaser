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
/// To choose the laser while the workflow runs, for example from the devices found by
/// <see cref="FindDevices"/>, connect a second input with the channel: <see cref="Channel"/> is then
/// ignored, the panel waits for the first channel, and each new channel starts a new session.
/// </remarks>
[Combinator]
[WorkflowElementCategory(ElementCategory.Combinator)]
[Description("Shows a control panel for an OBIS laser: displays its replies and emits the commands requested from the panel.")]
public sealed class ObisLaserVisualizer
{
    ObisLaserSession? session;

    [Description("The laser channel: 0 for a laser connected directly, 1 or more for lasers connected to an OBIS Remote or LaserBox. Ignored when the channel is given by a second input.")]
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

    /// <summary>
    /// Whether the workflow runs with the channel given by a second input, and no channel has arrived yet.
    /// </summary>
    internal bool AwaitingChannel => Volatile.Read(ref awaitingChannel);

    bool awaitingChannel;

    /// <summary>Raised when a session starts or ends.</summary>
    internal event EventHandler? SessionChanged;

    public IObservable<ObisCommand> Process(IObservable<ObisReply> source) => Observable.Create<ObisCommand>(observer =>
    {
        ValidatePollInterval();
        var channel = Channel;
        var current = StartSession(channel, observer);
        current.Item2.Add(source
            .Where(reply => reply.Command.Channel == channel)
            .Subscribe(current.Item1.Receive, observer.OnError, observer.OnCompleted));
        return Disposable.Create(() => EndSession(current));
    });

    /// <summary>
    /// Shows a control panel for the laser on the latest channel received: displays the replies to its
    /// commands, and emits the commands requested from the panel.
    /// </summary>
    /// <param name="source">The replies of the <see cref="Device"/> the laser is connected to.</param>
    /// <param name="channel">The channel of the laser. Each new channel starts a new session.</param>
    public IObservable<ObisCommand> Process(IObservable<ObisReply> source, IObservable<int> channel) => Observable.Create<ObisCommand>(observer =>
    {
        ValidatePollInterval();
        var gate = new object();
        var current = default(Tuple<ObisLaserSession, CompositeDisposable>);
        Volatile.Write(ref awaitingChannel, true);
        SessionChanged?.Invoke(this, EventArgs.Empty);

        var channelSubscription = channel.Subscribe(value =>
        {
            lock (gate)
            {
                if (current != null && current.Item1.Channel == value) return;
                if (current != null) EndSession(current);
                Volatile.Write(ref awaitingChannel, false);
                current = StartSession(value, observer);
            }
        }, observer.OnError);
        var replySubscription = source.Subscribe(reply =>
        {
            var active = Session;
            if (active != null && reply.Command.Channel == active.Channel) active.Receive(reply);
        }, observer.OnError, observer.OnCompleted);

        return Disposable.Create(() =>
        {
            channelSubscription.Dispose();
            replySubscription.Dispose();
            lock (gate)
            {
                if (current != null) EndSession(current);
                current = null;
            }
            Volatile.Write(ref awaitingChannel, false);
            SessionChanged?.Invoke(this, EventArgs.Empty);
        });
    });

    void ValidatePollInterval()
    {
        if (PollInterval <= TimeSpan.Zero) throw new InvalidOperationException("PollInterval must be positive.");
    }

    // Starts a session for the channel, with the subscriptions that end with it.
    Tuple<ObisLaserSession, CompositeDisposable> StartSession(int channel, IObserver<ObisCommand> observer)
    {
        var started = new ObisLaserSession(channel, () => Disable) { Polling = PollState };
        var subscriptions = new CompositeDisposable
        {
            started.Commands.Subscribe(observer.OnNext),
            Observable.Interval(PollInterval).Subscribe(_ => started.Poll()),
        };
        Volatile.Write(ref session, started);
        SessionChanged?.Invoke(this, EventArgs.Empty);
        return Tuple.Create(started, subscriptions);
    }

    void EndSession(Tuple<ObisLaserSession, CompositeDisposable> current)
    {
        current.Item2.Dispose();
        Interlocked.CompareExchange(ref session, null, current.Item1);
        SessionChanged?.Invoke(this, EventArgs.Empty);
    }
}

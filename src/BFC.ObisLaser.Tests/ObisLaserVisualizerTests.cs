using System.Reactive.Linq;
using System.Reactive.Subjects;
using ObisLaser.Net;
using Xunit;

namespace BFC.ObisLaser.Tests;

public class ObisLaserVisualizerTests
{
    static ObisReply Reply(CommandPayload payload, int channel, object? value, ObisDeviceException? error = null) =>
        new(payload.CreateCommand(channel), value, error, TimeSpan.FromMilliseconds(4));

    [Fact]
    public void CommandsRequestedFromThePanelAreEmittedForTheChannel()
    {
        var node = new ObisLaserVisualizer { Channel = 2 };
        var commands = new List<ObisCommand>();
        using var subscription = node.Process(Observable.Never<ObisReply>()).Subscribe(commands.Add);

        Assert.True(node.Session!.Send(new SetPower { Watts = 0.01 }));

        var command = Assert.Single(commands);
        Assert.Equal("SetPower(0.01 W)", command.Text);
        Assert.Equal(2, command.Channel);
    }

    [Fact]
    public void TheSessionExistsOnlyWhileTheWorkflowRuns()
    {
        var node = new ObisLaserVisualizer();
        var changes = 0;
        node.SessionChanged += (_, _) => changes++;
        Assert.Null(node.Session);

        using (node.Process(Observable.Never<ObisReply>()).Subscribe())
        {
            Assert.NotNull(node.Session);
        }

        Assert.Null(node.Session);
        Assert.Equal(2, changes);
    }

    [Fact]
    public void RepliesOfOtherChannelsAreIgnored()
    {
        var node = new ObisLaserVisualizer { Channel = 1 };
        using var replies = new Subject<ObisReply>();
        using var subscription = node.Process(replies).Subscribe();

        replies.OnNext(Reply(new GetPower(), 1, 0.005));
        replies.OnNext(Reply(new GetPower(), 2, 0.010));

        var entry = Assert.Single(node.Session!.GetLog());
        Assert.Equal("0.005", entry.Value);
    }

    [Fact]
    public void TheSessionKeepsTheLatestStateAndInformation()
    {
        var node = new ObisLaserVisualizer();
        using var replies = new Subject<ObisReply>();
        using var subscription = node.Process(replies).Subscribe();
        var state = new LaserState(true, OperatingMode.Cwp, 0.0055, 0.0058, 0.76, 23.4, StatusFlags.LaserEmission, FaultFlags.None);

        replies.OnNext(Reply(new GetState(), 0, state));

        Assert.Same(state, node.Session!.State);
        Assert.Null(node.Session.Info);
    }

    [Fact]
    public void RejectedCommandsAndInvalidParametersAreLoggedAsErrors()
    {
        var node = new ObisLaserVisualizer();
        using var replies = new Subject<ObisReply>();
        var commands = new List<ObisCommand>();
        using var subscription = node.Process(replies).Subscribe(commands.Add);

        replies.OnNext(Reply(new SetPower { Watts = 1 }, 0, null, new ObisDeviceException(-220, "Invalid parameter")));
        Assert.False(node.Session!.Send(new SetPower { Watts = -1 }));

        var log = node.Session.GetLog();
        Assert.Equal(2, log.Length);
        Assert.All(log, entry => Assert.False(entry.IsSuccess));
        Assert.Empty(commands);
    }

    [Fact]
    public void TheLogKeepsTheLatestEntries()
    {
        var session = new ObisLaserSession(0);
        for (int i = 0; i < ObisLaserSession.LogCapacity + 10; i++) session.Receive(Reply(new GetPower(), 0, (double)i));

        var log = session.GetLog();
        Assert.Equal(ObisLaserSession.LogCapacity, log.Length);
        Assert.Equal("10", log[0].Value);

        session.ClearLog();
        Assert.Empty(session.GetLog());
    }

    [Fact]
    public void PollingRequestsTheStateOnlyAfterThePreviousReply()
    {
        var session = new ObisLaserSession(0);
        var commands = new List<ObisCommand>();
        using var subscription = session.Commands.Subscribe(commands.Add);

        session.Poll();
        Assert.Empty(commands);

        session.Polling = true;
        session.Poll();
        session.Poll();
        Assert.Single(commands);

        session.Receive(new ObisReply(commands[0], null, new ObisDeviceException(-350, "Queue overflow"), TimeSpan.Zero));
        session.Poll();
        Assert.Equal(2, commands.Count);
    }

    [Fact]
    public async Task PollingSendsStateRequestsWhileTheWorkflowRuns()
    {
        var node = new ObisLaserVisualizer { PollState = true, PollInterval = TimeSpan.FromMilliseconds(10) };
        var command = await node.Process(Observable.Never<ObisReply>()).FirstAsync().Timeout(TimeSpan.FromSeconds(5));
        Assert.Equal("GetState", command.Name);
    }

    [Fact]
    public void TheSessionKeepsTheLatestValueOfEveryQuery()
    {
        var session = new ObisLaserSession(0);
        session.Receive(Reply(new GetHours(), 0, 4048.9));
        session.Receive(Reply(new GetAutoStart(), 0, true));
        session.Receive(Reply(new GetBaseplateTemperatureLimits(), 0, new TemperatureLimits(5, 44)));

        Assert.Equal(4048.9, session.GetValue<double>(nameof(GetHours)));
        Assert.True(session.GetValue<bool>(nameof(GetAutoStart)));
        Assert.Equal(44, session.GetValue<TemperatureLimits>(nameof(GetBaseplateTemperatureLimits))!.Value.High);
        Assert.Null(session.GetValue<double>(nameof(GetPower)));
    }

    [Fact]
    public void UnsupportedQueriesAreNotSentAgain()
    {
        var session = new ObisLaserSession(1);
        var commands = new List<ObisCommand>();
        using var subscription = session.Commands.Subscribe(commands.Add);

        session.Receive(Reply(new GetDiodeTemperature(), 1, null, new ObisDeviceException(-100, "SOUR1:TEMP:DIOD?")));
        session.Receive(new ObisReply(new GetDiodeWarmup().CreateCommand(1), null, new ObisProtocolException("Expected a single reply line, got 0: ''."), TimeSpan.Zero));
        session.Receive(Reply(new SetPower { Watts = 1 }, 1, null, new ObisDeviceException(-220, "SOUR1:POW:LEV:IMM:AMPL 1")));

        Assert.True(session.IsUnsupported(nameof(GetDiodeTemperature)));
        Assert.True(session.IsUnsupported(nameof(GetDiodeWarmup)));
        Assert.False(session.IsUnsupported(nameof(SetPower)));

        session.Send(new GetDiodeTemperature(), new GetDiodeWarmup(), new GetState());
        Assert.Equal(new[] { "GetState" }, commands.Select(command => command.Name));
    }

    [Fact]
    public void TheNodeIsACombinator()
    {
        var attribute = (Bonsai.WorkflowElementCategoryAttribute)Attribute.GetCustomAttribute(typeof(ObisLaserVisualizer), typeof(Bonsai.WorkflowElementCategoryAttribute))!;
        Assert.Equal(Bonsai.ElementCategory.Combinator, attribute.Category);
    }
}

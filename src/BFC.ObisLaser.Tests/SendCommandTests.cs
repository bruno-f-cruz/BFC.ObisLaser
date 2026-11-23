using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using ObisLaser.Net;
using Xunit;

namespace BFC.ObisLaser.Tests;

public class SendCommandTests
{
    [Fact]
    public async Task RepliesCarryTypedValuesInCommandOrder()
    {
        using var connection = FakeLaser.Connect(out _);
        var commands = new CommandPayload[] { new GetPower(), new GetEmission(), new GetState() }
            .Select(payload => payload.CreateCommand(0)).ToObservable();

        var replies = await new SendCommand().Process(commands, Observable.Return(connection)).ToList();

        Assert.All(replies, reply => Assert.True(reply.IsSuccess));
        Assert.Equal(0.01998, Assert.IsType<double>(replies[0].Value), 5);
        Assert.True(Assert.IsType<bool>(replies[1].Value));
        Assert.Equal(28.4, Assert.IsType<LaserState>(replies[2].Value).BaseplateTemperature, 5);
    }

    [Fact]
    public async Task CommandsAreSentToTheirChannel()
    {
        using var connection = FakeLaser.Connect(out var laser);
        var commands = Observable.Return(new GetPower().CreateCommand(2));

        var reply = await new SendCommand().Process(commands, Observable.Return(connection)).SingleAsync();

        Assert.Equal("SOUR2:POW:LEV?", laser.Written.Last());
        Assert.Equal(0.05, (double)reply.Value!, 5);
    }

    [Fact]
    public async Task RejectedCommandsAreEmittedAsFailedReplies()
    {
        using var connection = FakeLaser.Connect(out var laser);
        laser.Errors["SOUR:POW:LEV:IMM:AMPL 1.000000"] = -220;
        var commands = new CommandPayload[] { new SetPower { Watts = 1 }, new GetPower() }
            .Select(payload => payload.CreateCommand(0)).ToObservable();

        var replies = await new SendCommand().Process(commands, Observable.Return(connection)).ToList();

        Assert.False(replies[0].IsSuccess);
        Assert.Equal(-220, replies[0].ErrorCode);
        Assert.True(replies[1].IsSuccess);
    }

    [Fact]
    public async Task EnsureSuccessRaisesTheDeviceError()
    {
        using var connection = FakeLaser.Connect(out var laser);
        laser.Errors["SOUR:AM:STAT ON"] = -221;
        var commands = Observable.Return(new SetEmission { Enabled = true }.CreateCommand(0));

        var replies = new EnsureSuccess().Process(new SendCommand().Process(commands, Observable.Return(connection)));

        var error = await Assert.ThrowsAsync<ObisDeviceException>(async () => await replies.ToList());
        Assert.Equal(-221, error.Code);
    }

    [Fact]
    public async Task CommandsWaitForASharedConnectionThatArrivesLater()
    {
        using var connection = FakeLaser.Connect(out _);
        using var connections = new Subject<ObisConnection>();
        var commands = new ReplaySubject<ObisCommand>();
        commands.OnNext(new GetPower().CreateCommand(0));
        commands.OnCompleted();

        var task = new SendCommand().Process(commands, connections).ToList().ToTask();
        Assert.False(task.IsCompleted);
        connections.OnNext(connection);

        Assert.Single(await task);
    }

    [Fact]
    public void TooManyPendingCommandsRaiseAnError()
    {
        using var connection = FakeLaser.Connect(out _);
        using var connections = new Subject<ObisConnection>();
        using var commands = new Subject<ObisCommand>();
        Exception? error = null;
        using var subscription = new SendCommand { MaxPendingCommands = 2 }
            .Process(commands, connections)
            .Subscribe(_ => { }, e => error = e);

        for (int i = 0; i < 4; i++) commands.OnNext(new GetPower().CreateCommand(0));

        Assert.IsType<InvalidOperationException>(error);
    }

    [Fact]
    public void OwnedConnectionRequiresAPortName()
    {
        var replies = new SendCommand().Process(Observable.Return(new GetPower().CreateCommand(0)));
        Assert.Throws<InvalidOperationException>(() => replies.Wait());
    }

    [Theory]
    [InlineData(nameof(GetPower), typeof(double))]
    [InlineData(nameof(GetState), typeof(LaserState))]
    [InlineData(nameof(GetErrors), typeof(IReadOnlyList<ErrorRecord>))]
    public void ParseReplyOutputsTheResultTypeOfTheCommand(string command, Type expected)
    {
        var expression = new ParseReply { Command = command }.Build(new Expression[] { Expression.Constant(Observable.Empty<ObisReply>()) });
        Assert.Equal(typeof(IObservable<>).MakeGenericType(expected), expression.Type);
    }

    [Fact]
    public async Task ParseReplyKeepsOnlySuccessfulRepliesOfItsCommand()
    {
        using var connection = FakeLaser.Connect(out var laser);
        var commands = new CommandPayload[] { new GetPower(), new GetEmission(), new GetPower() }
            .Select(payload => payload.CreateCommand(0)).ToObservable();
        var replies = new SendCommand().Process(commands, Observable.Return(connection));
        var parse = new ParseReply { Command = nameof(GetPower) }.Build(new Expression[] { Expression.Constant(replies) });

        var powers = await Expression.Lambda<Func<IObservable<double>>>(parse).Compile()().ToList();

        Assert.Equal(2, powers.Count);
    }

    [Fact]
    public void ParseReplyRejectsCommandsWithoutValues()
    {
        var builder = new ParseReply { Command = nameof(SetPower) };
        Assert.Throws<InvalidOperationException>(() => builder.Build(new Expression[] { Expression.Constant(Observable.Empty<ObisReply>()) }));
    }
}

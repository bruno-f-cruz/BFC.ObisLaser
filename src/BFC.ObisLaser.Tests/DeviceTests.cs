using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reactive.Threading.Tasks;
using ObisLaser.Net;
using Xunit;

namespace BFC.ObisLaser.Tests;

public class DeviceTests
{
    static IObservable<ObisReply> Run(IObservable<ObisCommand> commands, FakeLaser laser, int maxPending = 64) =>
        new Device { MaxPendingCommands = maxPending }.Process(commands, () => new ObisConnection(laser, TimeSpan.FromMilliseconds(200)));

    static IObservable<ObisCommand> Commands(params CommandPayload[] payloads) =>
        payloads.Select(payload => payload.CreateCommand(0)).ToObservable();

    [Fact]
    public async Task RepliesCarryTheirCommandAndTypedValuesInOrder()
    {
        var laser = new FakeLaser();
        var replies = await Run(Commands(new GetPower(), new GetEmission(), new GetState(), new SetPower { Watts = 0.01 }), laser).ToList();

        Assert.Equal(new[] { "GetPower", "GetEmission", "GetState", "SetPower" }, replies.Select(reply => reply.Command.Name));
        Assert.Equal("SetPower(0.01 W)", replies[3].Command.Text);
        Assert.All(replies, reply => Assert.True(reply.IsSuccess));
        Assert.Equal(0.01998, Assert.IsType<double>(replies[0].Value), 5);
        Assert.True(Assert.IsType<bool>(replies[1].Value));
        Assert.Equal(28.4, Assert.IsType<LaserState>(replies[2].Value).BaseplateTemperature, 5);
        Assert.Null(replies[3].Value);
    }

    [Fact]
    public async Task EachCommandWaitsForThePreviousReply()
    {
        var laser = new FakeLaser { ReplyDelay = TimeSpan.FromMilliseconds(5) };
        using var commands = new Subject<ObisCommand>();
        var task = Run(commands, laser).ToList().ToTask();

        // A burst: all commands arrive before the first reply.
        for (int i = 0; i < 10; i++) commands.OnNext(new SetPower { Watts = i / 1000.0 }.CreateCommand(0));
        commands.OnCompleted();
        var replies = await task;

        Assert.Equal(Enumerable.Range(0, 10).Select(i => $"SetPower({i / 1000.0} W)"), replies.Select(reply => reply.Command.Text));
        var exchanges = laser.Log.SkipWhile(entry => entry.StartsWith("> SYST:COMM:HAND") || entry.StartsWith("< ON") || entry == "< OK").ToList();
        for (int i = 0; i < exchanges.Count; i += 2)
        {
            Assert.StartsWith("> SOUR:POW:LEV:IMM:AMPL", exchanges[i]);
            Assert.Equal("< OK", exchanges[i + 1]);
        }
    }

    [Fact]
    public async Task CommandsAreSentToTheirChannel()
    {
        var laser = new FakeLaser();
        var reply = await Run(Observable.Return(new GetPower().CreateCommand(2)), laser).SingleAsync();

        Assert.Equal("SOUR2:POW:LEV?", laser.Written.Last());
        Assert.Equal(2, reply.Command.Channel);
        Assert.Equal(0.05, (double)reply.Value!, 5);
    }

    [Fact]
    public async Task RejectedCommandsAreEmittedAsFailedReplies()
    {
        var laser = new FakeLaser();
        laser.Errors["SOUR:POW:LEV:IMM:AMPL 1.000000"] = -220;

        var replies = await Run(Commands(new SetPower { Watts = 1 }, new GetPower()), laser).ToList();

        Assert.False(replies[0].IsSuccess);
        Assert.Equal(-220, replies[0].ErrorCode);
        Assert.Equal("SetPower", replies[0].Command.Name);
        Assert.True(replies[1].IsSuccess);
    }

    [Fact]
    public async Task EnsureSuccessRaisesTheDeviceError()
    {
        var laser = new FakeLaser();
        laser.Errors["SOUR:AM:STAT ON"] = -221;

        var replies = new EnsureSuccess().Process(Run(Commands(new SetEmission { Enabled = true }), laser));

        var error = await Assert.ThrowsAsync<ObisDeviceException>(async () => await replies.ToList());
        Assert.Equal(-221, error.Code);
    }

    [Fact]
    public async Task AMissingReplyRaisesATimeout()
    {
        var laser = new FakeLaser();
        var connection = new ObisConnection(laser, TimeSpan.FromMilliseconds(50));
        laser.Silent = true;

        var replies = new Device().Process(Commands(new GetPower()), () => connection);

        await Assert.ThrowsAsync<ObisTimeoutException>(async () => await replies.ToList());
    }

    [Fact]
    public void TooManyPendingCommandsRaiseAnError()
    {
        var laser = new FakeLaser { ReplyDelay = TimeSpan.FromMilliseconds(50) };
        using var commands = new Subject<ObisCommand>();
        Exception? error = null;
        using var subscription = Run(commands, laser, maxPending: 2).Subscribe(_ => { }, e => error = e);

        for (int i = 0; i < 4; i++) commands.OnNext(new GetPower().CreateCommand(0));

        Assert.IsType<InvalidOperationException>(error);
    }

    [Fact]
    public async Task ThePortIsClosedWhenTheWorkflowStops()
    {
        var laser = new FakeLaser();
        await Run(Commands(new GetPower()), laser).ToList();
        Assert.True(laser.Disposed);
    }

    [Fact]
    public void APortNameIsRequired()
    {
        var replies = new Device().Process(Commands(new GetPower()));
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
        var laser = new FakeLaser();
        var replies = Run(Commands(new GetPower(), new GetEmission(), new GetPower()), laser);
        var parse = new ParseReply { Command = nameof(GetPower) }.Build(new Expression[] { Expression.Constant(replies) });

        var powers = await Expression.Lambda<Func<IObservable<double>>>(parse).Compile()().ToList();

        Assert.Equal(2, powers.Count);
    }

    [Fact]
    public async Task ParseReplyCanKeepOnlyOneChannel()
    {
        var laser = new FakeLaser();
        var commands = new[] { 0, 2, 0 }.Select(channel => new GetPower().CreateCommand(channel)).ToObservable();
        var replies = Run(commands, laser);
        var parse = new ParseReply { Command = nameof(GetPower), Channel = 2 }.Build(new Expression[] { Expression.Constant(replies) });

        var powers = await Expression.Lambda<Func<IObservable<double>>>(parse).Compile()().ToList();

        Assert.Equal(0.05, Assert.Single(powers), 5);
    }

    [Fact]
    public void ParseReplyRejectsCommandsWithoutValues()
    {
        var builder = new ParseReply { Command = nameof(SetPower) };
        Assert.Throws<InvalidOperationException>(() => builder.Build(new Expression[] { Expression.Constant(Observable.Empty<ObisReply>()) }));
    }
}

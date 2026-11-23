using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using ObisLaser.Net;
using Xunit;

namespace BFC.ObisLaser.Tests;

/// <summary>
/// Tests against a real OBIS laser. Skipped unless <c>OBIS_PORT</c> is set, and always skipped when
/// <c>CI</c> is set. <c>OBIS_CHANNEL</c> is the channel of the laser behind a controller (default 0).
/// No test turns emission on, and settings changed by a test are restored.
/// </summary>
/// <example>
/// <code>
/// $env:OBIS_PORT = "COM3"; $env:OBIS_CHANNEL = "1"
/// dotnet test --filter "FullyQualifiedName~HardwareTests"
/// </code>
/// </example>
public class HardwareTests
{
    static readonly string? Port = Environment.GetEnvironmentVariable("OBIS_PORT");
    static readonly int Channel = int.TryParse(Environment.GetEnvironmentVariable("OBIS_CHANNEL"), out var channel) ? channel : 0;

    static Task<IList<ObisReply>> Send(params CommandPayload[] payloads) =>
        new Device { PortName = Port }
            .Process(payloads.Select(payload => payload.CreateCommand(Channel)).ToObservable())
            .ToList()
            .ToTask();

    [HardwareFact]
    public async Task DeviceRepliesToEachCommandInOrder()
    {
        var replies = await Send(new Identify(), new GetInfo(), new GetState(), new GetPower());

        Assert.All(replies, reply => Assert.True(reply.IsSuccess, reply.Error?.Message));
        Assert.Equal(new[] { "Identify", "GetInfo", "GetState", "GetPower" }, replies.Select(reply => reply.Command.Name));
        Assert.StartsWith("Coherent", Assert.IsType<string>(replies[0].Value));
        Assert.True(Assert.IsType<DeviceInfo>(replies[1].Value).MaxPower > 0);
        Assert.IsType<LaserState>(replies[2].Value);
        Assert.IsType<double>(replies[3].Value);
        foreach (var reply in replies) Console.WriteLine($"{reply.Command.Text} ({reply.Duration.TotalMilliseconds:F1} ms): {reply.Value}");
    }

    [HardwareFact]
    public async Task RejectedCommandsAreReplies()
    {
        var info = (DeviceInfo)(await Send(new GetInfo())).Single().Value!;
        var replies = await Send(new SetPower { Watts = info.MaxPower * 10 }, new GetPower());

        Assert.False(replies[0].IsSuccess);
        Assert.NotNull(replies[0].ErrorCode);
        Assert.True(replies[1].IsSuccess);
    }

    [HardwareFact]
    public async Task PowerSetpointRoundTrips()
    {
        var original = (double)(await Send(new GetPowerSetpoint())).Single().Value!;
        var info = (DeviceInfo)(await Send(new GetInfo())).Single().Value!;
        var target = Math.Round(info.MaxPower * 0.05, 4);
        try
        {
            var replies = await Send(new SetPower { Watts = target }, new GetPowerSetpoint());
            Assert.True(replies[0].IsSuccess, replies[0].Error?.Message);
            Assert.Equal(target, (double)replies[1].Value!, 3);
        }
        finally
        {
            await Send(new SetPower { Watts = original });
        }
    }

    [HardwareFact]
    public async Task FindDevicesFindsTheLaser()
    {
        var devices = await new FindDevices().Generate().ToList();
        foreach (var device in devices) Console.WriteLine(device);
        Assert.Contains(devices, device => string.Equals(device.PortName, Port, StringComparison.OrdinalIgnoreCase) && device.Channel == Channel);
    }
}

/// <summary>A test that runs only against a real laser, outside CI.</summary>
public sealed class HardwareFactAttribute : FactAttribute
{
    public HardwareFactAttribute()
    {
        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CI"))) Skip = "Hardware tests never run in CI.";
        else if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OBIS_PORT"))) Skip = "Set OBIS_PORT to test with a real laser.";
    }
}

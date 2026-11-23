using ObisLaser.Net;

namespace BFC.ObisLaser.Tests;

/// <summary>
/// Replies to commands like an OBIS laser, from a table of canned query replies. Unknown queries,
/// and commands listed in <see cref="Errors"/>, are rejected with <c>ERR&lt;code&gt;</c>.
/// </summary>
sealed class FakeLaser : IObisTransport
{
    readonly Queue<string> output = new();
    readonly object gate = new();

    public Dictionary<string, string> Replies { get; } = new()
    {
        ["SYST:COMM:HAND?"] = "ON",
        ["SOUR:AM:STAT?"] = "ON",
        ["SOUR:AM:SOUR?"] = "CWP",
        ["SOUR:POW:LEV:IMM:AMPL?"] = "0.02000",
        ["SOUR:POW:LEV?"] = "0.01998",
        ["SOUR:POW:CURR?"] = "0.06100",
        ["SOUR:TEMP:BAS?"] = "28.4C",
        ["SYST:STAT?"] = "00000006",
        ["SYST:FAUL?"] = "00000000",
        ["SOUR2:POW:LEV?"] = "0.05000",
    };

    public Dictionary<string, int> Errors { get; } = new();

    public List<string> Written { get; } = new();

    public bool Disposed { get; private set; }

    public static ObisConnection Connect(out FakeLaser laser)
    {
        laser = new FakeLaser();
        return new ObisConnection(laser, TimeSpan.FromMilliseconds(200));
    }

    public void Write(string text)
    {
        lock (gate)
        {
            var command = text.TrimEnd('\r');
            Written.Add(command);
            if (Errors.TryGetValue(command, out var code)) output.Enqueue("ERR" + code);
            else if (!command.Contains("?")) output.Enqueue("OK");
            else if (Replies.TryGetValue(command, out var reply))
            {
                output.Enqueue(reply);
                output.Enqueue("OK");
            }
            else output.Enqueue("ERR-100");
        }
    }

    public string ReadLine()
    {
        lock (gate)
        {
            if (output.Count == 0) throw new TimeoutException();
            return output.Dequeue();
        }
    }

    public void DiscardInBuffer()
    {
        lock (gate) output.Clear();
    }

    public void Dispose() => Disposed = true;
}

using System.ComponentModel;
using System.Reactive.Linq;
using Bonsai;

namespace BFC.ObisLaser;

/// <summary>
/// Raises a sequence error when the laser rejected a command.
/// </summary>
/// <remarks>
/// <see cref="SendCommand"/> emits rejected commands as replies so that a running experiment is not stopped
/// by one invalid setting. Place this node after it to stop on any rejection instead.
/// </remarks>
[Description("Raises an error when the laser rejected a command.")]
public sealed class EnsureSuccess : Combinator<ObisReply, ObisReply>
{
    public override IObservable<ObisReply> Process(IObservable<ObisReply> source) =>
        source.Select(reply => reply.IsSuccess ? reply : throw reply.Error!);
}

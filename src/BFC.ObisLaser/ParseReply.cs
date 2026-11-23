using System.ComponentModel;
using System.Globalization;
using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Reflection;
using Bonsai;
using Bonsai.Expressions;

namespace BFC.ObisLaser;

/// <summary>
/// Emits the typed values of successful replies to one kind of command, ignoring all other replies.
/// </summary>
/// <remarks>
/// The selected command determines the output type, e.g. <see cref="GetState"/> outputs
/// <see cref="global::ObisLaser.Net.LaserState"/> and <see cref="GetPower"/> outputs <see cref="double"/>.
/// Set <see cref="Channel"/> to keep only the replies of one laser behind an OBIS Remote or LaserBox.
/// Failed replies are skipped; use <see cref="EnsureSuccess"/> upstream to raise an error for them.
/// </remarks>
[DefaultProperty(nameof(Command))]
[WorkflowElementCategory(ElementCategory.Combinator)]
[Description("Emits the typed values of successful replies to one kind of command, ignoring all other replies.")]
public sealed class ParseReply : SingleArgumentExpressionBuilder, INamedElement
{
    [TypeConverter(typeof(QueryNameConverter))]
    [RefreshProperties(RefreshProperties.All)]
    [Description("The command whose replies to emit. It determines the output type. Selected when the workflow is built.")]
    public string Command { get; set; } = nameof(GetState);

    [Description("Only emit replies from this laser channel. Leave empty to emit replies from every channel.")]
    public int? Channel { get; set; }

    string INamedElement.Name => Command;

    public override Expression Build(IEnumerable<Expression> arguments)
    {
        var commandType = CommandTypes.Find(Command) ?? throw new InvalidOperationException($"Unknown command '{Command}'.");
        var resultType = CommandTypes.GetResultType(commandType)
            ?? throw new InvalidOperationException($"{commandType.Name} replies carry no value; filter replies by Command.Name instead.");
        var parse = typeof(ParseReply).GetMethod(nameof(Parse), BindingFlags.NonPublic | BindingFlags.Static)!;
        return Expression.Call(parse.MakeGenericMethod(resultType), arguments.Single(), Expression.Constant(commandType.Name), Expression.Constant(Channel, typeof(int?)));
    }

    static IObservable<T> Parse<T>(IObservable<ObisReply> source, string name, int? channel) =>
        source.Where(reply => reply.IsSuccess && reply.Command.Name == name && (channel is null || reply.Command.Channel == channel))
            .Select(reply => (T)reply.Value!);

    // Lists the commands whose replies carry a value.
    sealed class QueryNameConverter : StringConverter
    {
        public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;

        public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => true;

        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context) =>
            new(CommandTypes.All.Where(type => CommandTypes.GetResultType(type) is not null).Select(type => type.Name).ToArray());
    }
}

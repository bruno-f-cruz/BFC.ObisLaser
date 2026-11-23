using System.ComponentModel;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Xml;
using Bonsai;
using Bonsai.Expressions;
using ObisLaser.Net;
using Xunit;

namespace BFC.ObisLaser.Tests;

public class CreateCommandTests
{
    public static IEnumerable<object[]> AllCommands() => CommandTypes.All.Select(type => new object[] { type });

    [Fact]
    public async Task WithoutInputEmitsOneCommandAndCompletes()
    {
        var node = new CreateCommand { Command = new SetPower { Watts = 0.01 }, Channel = 2 };
        var commands = await Build<ObisCommand>(node).ToList();
        var command = Assert.Single(commands);
        Assert.Equal("SetPower", command.Name);
        Assert.Equal(2, command.Channel);
    }

    [Fact]
    public void WithInputEmitsOneCommandPerNotificationWithTheValuesAtThatTime()
    {
        var payload = new SetPower { Watts = 0.01 };
        var node = new CreateCommand { Command = payload };
        using var trigger = new Subject<int>();
        var commands = new List<ObisCommand>();
        var source = (IObservable<ObisCommand>)typeof(CreateCommand)
            .GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Single(method => method.Name == "Generate" && method.IsGenericMethod)
            .MakeGenericMethod(typeof(int))
            .Invoke(node, new object[] { trigger })!;
        using var subscription = source.Subscribe(commands.Add);

        trigger.OnNext(0);
        payload.Watts = 0.02;
        trigger.OnNext(1);

        Assert.Equal(new[] { "SetPower(0.01 W)", "SetPower(0.02 W)" }, commands.Select(command => command.Text));
    }

    [Fact]
    public void InvalidParametersFailWhenTheCommandIsCreated()
    {
        var node = new CreateCommand { Command = new SetPower { Watts = -1 } };
        Assert.Throws<ArgumentOutOfRangeException>(() => Build<ObisCommand>(node).Wait());
    }

    [Fact]
    public void PropertyGridShowsTheCommandAsATypeFollowedByItsParameters()
    {
        var node = new CreateCommand { Command = new SetPower() };
        var properties = TypeDescriptor.GetProperties(node);
        Assert.Equal(typeof(Type), properties[nameof(CreateCommand.Command)]!.PropertyType);
        Assert.NotNull(properties[nameof(SetPower.Watts)]);
        Assert.NotNull(properties[nameof(CreateCommand.Channel)]);

        properties[nameof(SetPower.Watts)]!.SetValue(node, 0.03);
        Assert.Equal(0.03, ((SetPower)node.Command).Watts);
    }

    [Fact]
    public void SwitchingCommandsKeepsParametersWithTheSameName()
    {
        var node = new CreateCommand { Command = new SetEmission { Enabled = true } };
        TypeDescriptor.GetProperties(node)[nameof(CreateCommand.Command)]!.SetValue(node, typeof(SetAutoStart));
        Assert.True(Assert.IsType<SetAutoStart>(node.Command).Enabled);
    }

    [Fact]
    public void DropDownListsEveryCommand()
    {
        var descriptor = TypeDescriptor.GetProperties(new CreateCommand())[nameof(CreateCommand.Command)]!;
        var values = descriptor.Converter.GetStandardValues()!.Cast<Type>().ToList();
        Assert.Equal(CommandTypes.All, values);
        Assert.Equal("SetPower", descriptor.Converter.ConvertToString(typeof(SetPower)));
        Assert.Equal(typeof(SetPower), descriptor.Converter.ConvertFromString("SetPower"));
    }

    [Theory]
    [MemberData(nameof(AllCommands))]
    public void EveryCommandRoundTripsThroughTheBonsaiSerializer(Type commandType)
    {
        var workflow = new WorkflowBuilder();
        workflow.Workflow.Add(new CreateCommand { Command = (CommandPayload)Activator.CreateInstance(commandType)!, Channel = 3 });
        var restored = RoundTrip(workflow);
        var node = Assert.IsType<CreateCommand>(restored.Workflow.Single().Value);
        Assert.IsType(commandType, node.Command);
        Assert.Equal(3, node.Channel);
    }

    [Theory]
    [MemberData(nameof(AllCommands))]
    public void EveryCommandCanBeCreatedWithItsDefaults(Type commandType)
    {
        var payload = (CommandPayload)Activator.CreateInstance(commandType)!;
        var command = payload.CreateCommand(0);
        Assert.Equal(commandType.Name, command.Name);
        Assert.False(string.IsNullOrEmpty(TypeDescriptor.GetAttributes(commandType).OfType<DescriptionAttribute>().Single().Description));
    }

    [Fact]
    public void ParametersRoundTripThroughTheBonsaiSerializer()
    {
        var workflow = new WorkflowBuilder();
        workflow.Workflow.Add(new CreateCommand { Command = new SetOperatingMode { Mode = OperatingMode.Analog } });
        workflow.Workflow.Add(new CreateCommand { Command = new SetUserInfo { Index = 2, Text = "rig 3" } });
        var restored = RoundTrip(workflow).Workflow.Select(node => ((CreateCommand)node.Value).Command).ToList();
        Assert.Equal(OperatingMode.Analog, Assert.IsType<SetOperatingMode>(restored[0]).Mode);
        Assert.Equal("rig 3", Assert.IsType<SetUserInfo>(restored[1]).Text);
    }

    [Fact]
    public async Task MappedPropertiesSetTheCommandParameters()
    {
        var workflow = new WorkflowBuilder();
        var values = workflow.Workflow.Add(new CombinatorBuilder { Combinator = new Values { Items = new[] { 0.01, 0.02 } } });
        var mapping = workflow.Workflow.Add(new InputMappingBuilder { PropertyMappings = { new PropertyMapping(nameof(SetPower.Watts), "") } });
        var create = workflow.Workflow.Add(new CreateCommand { Command = new SetPower() });
        var output = workflow.Workflow.Add(new WorkflowOutputBuilder());
        workflow.Workflow.AddEdge(values, mapping, new ExpressionBuilderArgument());
        workflow.Workflow.AddEdge(mapping, create, new ExpressionBuilderArgument());
        workflow.Workflow.AddEdge(create, output, new ExpressionBuilderArgument());

        var commands = await workflow.Workflow.BuildObservable<ObisCommand>().ToList();

        Assert.Equal(new[] { "SetPower(0.01 W)", "SetPower(0.02 W)" }, commands.Select(command => command.Text));
    }

    static IObservable<T> Build<T>(CreateCommand node)
    {
        var workflow = new WorkflowBuilder();
        var create = workflow.Workflow.Add(node);
        var output = workflow.Workflow.Add(new WorkflowOutputBuilder());
        workflow.Workflow.AddEdge(create, output, new ExpressionBuilderArgument());
        return workflow.Workflow.BuildObservable<T>();
    }

    internal static WorkflowBuilder RoundTrip(WorkflowBuilder workflow)
    {
        using var writer = new StringWriter();
        WorkflowBuilder.Serializer.Serialize(writer, workflow);
        using var reader = XmlReader.Create(new StringReader(writer.ToString()));
        return (WorkflowBuilder)WorkflowBuilder.Serializer.Deserialize(reader)!;
    }
}

/// <summary>Emits a fixed list of values.</summary>
public sealed class Values : Source<double>
{
    public double[] Items { get; set; } = Array.Empty<double>();

    public override IObservable<double> Generate() => Items.ToObservable();
}

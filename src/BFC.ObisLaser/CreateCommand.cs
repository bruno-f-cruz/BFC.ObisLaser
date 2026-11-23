using System.ComponentModel;
using System.Linq.Expressions;
using System.Reactive.Linq;
using System.Xml.Serialization;
using Bonsai;
using Bonsai.Expressions;

namespace BFC.ObisLaser;

/// <summary>
/// Creates OBIS laser commands, once when the workflow starts or for each input notification.
/// </summary>
/// <remarks>
/// Select the command with the <see cref="Command"/> property; its parameters, such as the power of
/// <see cref="SetPower"/>, appear alongside it and can be mapped from upstream values.
/// </remarks>
[DefaultProperty(nameof(Command))]
[WorkflowElementCategory(ElementCategory.Source)]
[XmlInclude(typeof(Identify))]
[XmlInclude(typeof(GetInfo))]
[XmlInclude(typeof(GetUserInfo))]
[XmlInclude(typeof(SetUserInfo))]
[XmlInclude(typeof(GetFieldCalibrationDate))]
[XmlInclude(typeof(SetFieldCalibrationDate))]
[XmlInclude(typeof(GetEmission))]
[XmlInclude(typeof(SetEmission))]
[XmlInclude(typeof(GetPowerSetpoint))]
[XmlInclude(typeof(SetPower))]
[XmlInclude(typeof(GetPower))]
[XmlInclude(typeof(GetCurrent))]
[XmlInclude(typeof(GetOperatingMode))]
[XmlInclude(typeof(SetOperatingMode))]
[XmlInclude(typeof(GetState))]
[XmlInclude(typeof(GetStatus))]
[XmlInclude(typeof(GetFaults))]
[XmlInclude(typeof(GetBaseplateTemperature))]
[XmlInclude(typeof(GetDiodeTemperature))]
[XmlInclude(typeof(GetDiodeTemperatureSetpoint))]
[XmlInclude(typeof(GetInternalTemperature))]
[XmlInclude(typeof(GetBaseplateTemperatureLimits))]
[XmlInclude(typeof(GetDiodeTemperatureLimits))]
[XmlInclude(typeof(GetInternalTemperatureLimits))]
[XmlInclude(typeof(GetThresholdCurrent))]
[XmlInclude(typeof(GetMaxCurrent))]
[XmlInclude(typeof(GetInterlock))]
[XmlInclude(typeof(GetNoise))]
[XmlInclude(typeof(GetPowerCycles))]
[XmlInclude(typeof(GetHours))]
[XmlInclude(typeof(GetDiodeHours))]
[XmlInclude(typeof(GetErrors))]
[XmlInclude(typeof(ClearErrors))]
[XmlInclude(typeof(GetAutoStart))]
[XmlInclude(typeof(SetAutoStart))]
[XmlInclude(typeof(GetCdrhDelay))]
[XmlInclude(typeof(SetCdrhDelay))]
[XmlInclude(typeof(GetTecEnabled))]
[XmlInclude(typeof(SetTecEnabled))]
[XmlInclude(typeof(GetDiodeWarmup))]
[XmlInclude(typeof(SetDiodeWarmup))]
[XmlInclude(typeof(GetStatusIndicator))]
[XmlInclude(typeof(SetStatusIndicator))]
[XmlInclude(typeof(GetAnalogBlanking))]
[XmlInclude(typeof(SetAnalogBlanking))]
[XmlInclude(typeof(GetAnalogImpedance))]
[XmlInclude(typeof(SetAnalogImpedance))]
[XmlInclude(typeof(Reset))]
[XmlInclude(typeof(SelfTest))]
[XmlInclude(typeof(StartFieldCalibration))]
[XmlInclude(typeof(UndoFieldCalibration))]
[XmlInclude(typeof(Recover))]
[XmlInclude(typeof(Query))]
[XmlInclude(typeof(Broadcast))]
[Description("Creates OBIS laser commands, once when the workflow starts or for each input notification.")]
public sealed class CreateCommand : ExpressionBuilder, INamedElement, ICustomTypeDescriptor
{
    static readonly Bonsai.Range<int> argumentRange = Bonsai.Range.Create(lowerBound: 0, upperBound: 1);

    public override Bonsai.Range<int> ArgumentRange => argumentRange;

    [Description("The laser channel: 0 for a laser connected directly, 1 or more for lasers connected to an OBIS Remote or LaserBox.")]
    public int Channel { get; set; }

    [DesignOnly(true)]
    [Externalizable(false)]
    [RefreshProperties(RefreshProperties.All)]
    [TypeConverter(typeof(CommandTypeConverter))]
    [Description("The command to create. Its parameters appear below.")]
    public CommandPayload Command { get; set; } = new GetState();

    string INamedElement.Name => Command?.Name ?? nameof(CreateCommand);

    public override Expression Build(IEnumerable<Expression> arguments)
    {
        var source = arguments.FirstOrDefault();
        var builder = Expression.Constant(this);
        if (source is null) return Expression.Call(builder, nameof(Generate), null);
        var sourceType = source.Type.GetGenericArguments()[0];
        return Expression.Call(builder, nameof(Generate), new[] { sourceType }, source);
    }

    IObservable<ObisCommand> Generate() => Observable.Defer(() => Observable.Return(CreateMessage()));

    IObservable<ObisCommand> Generate<TSource>(IObservable<TSource> source) => source.Select(_ => CreateMessage());

    ObisCommand CreateMessage()
    {
        var command = Command ?? throw new InvalidOperationException("Select a command to create.");
        return command.CreateCommand(Channel);
    }

    #region ICustomTypeDescriptor Members

    // Shows the selected command as a drop-down of types and merges its properties into this node,
    // adapted from Bonsai.Harp (https://github.com/bonsai-rx/harp, MIT license).

    AttributeCollection ICustomTypeDescriptor.GetAttributes()
    {
        var attributes = TypeDescriptor.GetAttributes(GetType());
        return TypeDescriptor.GetAttributes(Command)[typeof(DescriptionAttribute)] is DescriptionAttribute description
            ? AttributeCollection.FromExisting(attributes, description)
            : attributes;
    }

    string ICustomTypeDescriptor.GetClassName() => TypeDescriptor.GetClassName(GetType());

    string? ICustomTypeDescriptor.GetComponentName() => null;

    TypeConverter ICustomTypeDescriptor.GetConverter() => TypeDescriptor.GetConverter(GetType());

    EventDescriptor? ICustomTypeDescriptor.GetDefaultEvent() => null;

    PropertyDescriptor ICustomTypeDescriptor.GetDefaultProperty() => new CommandTypePropertyDescriptor(CommandProperty);

    object ICustomTypeDescriptor.GetEditor(Type editorBaseType) => TypeDescriptor.GetEditor(GetType(), editorBaseType);

    EventDescriptorCollection ICustomTypeDescriptor.GetEvents() => EventDescriptorCollection.Empty;

    EventDescriptorCollection ICustomTypeDescriptor.GetEvents(Attribute[] attributes) => EventDescriptorCollection.Empty;

    PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties() => ((ICustomTypeDescriptor)this).GetProperties(Array.Empty<Attribute>());

    PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties(Attribute[] attributes)
    {
        var properties = TypeDescriptor.GetProperties(GetType(), attributes).Cast<PropertyDescriptor>()
            .Select(property => property.Name == nameof(Command) ? new CommandTypePropertyDescriptor(property) : property);
        var commandProperties = Command is null
            ? Enumerable.Empty<PropertyDescriptor>()
            : TypeDescriptor.GetProperties(Command, attributes).Cast<PropertyDescriptor>();
        return new PropertyDescriptorCollection(properties.Concat(commandProperties).ToArray());
    }

    // Properties not declared by this node belong to the selected command; property mappings use this too.
    object ICustomTypeDescriptor.GetPropertyOwner(PropertyDescriptor? pd) =>
        pd?.ComponentType.IsAssignableFrom(GetType()) == true ? this : Command;

    static PropertyDescriptor CommandProperty => TypeDescriptor.GetProperties(typeof(CreateCommand))[nameof(Command)]!;

    // Exposes the command as its type, so the property grid offers the list of commands, and keeps
    // parameter values with the same name and type when switching between commands.
    sealed class CommandTypePropertyDescriptor : PropertyDescriptor
    {
        readonly PropertyDescriptor descriptor;

        public CommandTypePropertyDescriptor(PropertyDescriptor descriptor)
            : base(descriptor)
        {
            this.descriptor = descriptor;
        }

        public override Type ComponentType => descriptor.ComponentType;

        public override bool IsReadOnly => false;

        public override Type PropertyType => typeof(Type);

        public override bool CanResetValue(object component) => false;

        public override object? GetValue(object? component) => descriptor.GetValue(component)?.GetType();

        public override void ResetValue(object component)
        {
        }

        public override void SetValue(object? component, object? value)
        {
            if (value is not Type type) return;
            var current = descriptor.GetValue(component);
            var next = Activator.CreateInstance(type)!;
            if (current is not null)
            {
                var currentProperties = TypeDescriptor.GetProperties(current);
                foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(next))
                {
                    var match = currentProperties[property.Name];
                    if (match?.PropertyType == property.PropertyType && !property.IsReadOnly)
                    {
                        property.SetValue(next, match.GetValue(current));
                    }
                }
            }

            descriptor.SetValue(component, next);
        }

        public override bool ShouldSerializeValue(object component) => true;
    }

    #endregion
}

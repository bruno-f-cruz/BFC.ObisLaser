using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Xml.Serialization;

namespace BFC.ObisLaser;

/// <summary>
/// Lists the commands available in <see cref="CreateCommand"/>: the types declared with
/// <see cref="XmlIncludeAttribute"/> on it, which are also the types it can serialize.
/// </summary>
static class CommandTypes
{
    public static IReadOnlyList<Type> All { get; } = typeof(CreateCommand)
        .GetCustomAttributes<XmlIncludeAttribute>()
        .Select(attribute => attribute.Type!)
        .ToArray();

    public static Type? Find(string? name) =>
        All.FirstOrDefault(type => string.Equals(type.Name, name, StringComparison.OrdinalIgnoreCase));

    public static Type? GetResultType(Type commandType) =>
        ((CommandPayload)Activator.CreateInstance(commandType)!).ResultType;
}

/// <summary>
/// Converts between command types and their names for the <see cref="CreateCommand.Command"/> drop-down.
/// </summary>
sealed class CommandTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType) =>
        sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) =>
        value is string name ? CommandTypes.Find(name) : base.ConvertFrom(context, culture, value);

    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType) =>
        destinationType == typeof(string) && value is Type type ? type.Name : base.ConvertTo(context, culture, value, destinationType);

    public override bool GetStandardValuesSupported(ITypeDescriptorContext? context) => true;

    public override bool GetStandardValuesExclusive(ITypeDescriptorContext? context) => true;

    public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext? context) =>
        new(CommandTypes.All.ToArray());
}

using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Sotsera.Rafter;

internal sealed class OptionBindingStrategy
{
    private static readonly MethodInfo CreateParsableConverterMethod = typeof(OptionBindingStrategy)
        .GetMethod(nameof(CreateParsableConverter), BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly Func<string, ConversionAttempt> _convert;
    private readonly Func<IReadOnlyList<object?>, object> _createImmutableList;

    private OptionBindingStrategy(
        Func<string, ConversionAttempt> convert,
        Func<IReadOnlyList<object?>, object> createImmutableList,
        ImmutableArray<string> permittedValues)
    {
        _convert = convert;
        _createImmutableList = createImmutableList;
        PermittedValues = permittedValues;
    }

    internal ImmutableArray<string> PermittedValues { get; }

    internal static OptionBindingStrategy Create<T>()
    {
        Type valueType = typeof(T);
        Type effectiveType = Nullable.GetUnderlyingType(valueType) ?? valueType;
        Func<string, ConversionAttempt> converter;
        ImmutableArray<string> permittedValues = [];

        if (effectiveType == typeof(string))
        {
            converter = static value => ConversionAttempt.Succeeded(value);
        }
        else if (effectiveType == typeof(bool))
        {
            converter = ConvertBoolean;
            permittedValues = ["true", "false"];
        }
        else if (effectiveType.IsEnum)
        {
            (converter, permittedValues) = CreateEnumConverter(effectiveType);
        }
        else if (effectiveType.GetInterfaces().Any(interfaceType =>
            interfaceType.IsGenericType
            && interfaceType.GetGenericTypeDefinition() == typeof(IParsable<>)
            && interfaceType.GenericTypeArguments[0] == effectiveType))
        {
            converter = (Func<string, ConversionAttempt>)CreateParsableConverterMethod
                .MakeGenericMethod(effectiveType)
                .Invoke(null, null)!;
        }
        else
        {
            converter = static _ => ConversionAttempt.Failed;
        }

        return new OptionBindingStrategy(converter, values => CreateImmutableList<T>(values), permittedValues);
    }

    internal ConversionAttempt Convert(string value) => _convert(value);

    internal object CreateImmutableList(IReadOnlyList<object?> values) => _createImmutableList(values);

    private static ConversionAttempt ConvertBoolean(string value)
    {
        if (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase))
        {
            return ConversionAttempt.Succeeded(true);
        }

        if (string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
        {
            return ConversionAttempt.Succeeded(false);
        }

        return ConversionAttempt.Failed;
    }

    private static (Func<string, ConversionAttempt> Convert, ImmutableArray<string> Names) CreateEnumConverter(Type enumType)
    {
        FieldInfo[] fields = enumType.GetFields(BindingFlags.Public | BindingFlags.Static)
            .OrderBy(static field => field.MetadataToken)
            .ToArray();
        Dictionary<string, object> values = new(StringComparer.OrdinalIgnoreCase);
        foreach (FieldInfo field in fields)
        {
            _ = values.TryAdd(field.Name, field.GetValue(null)!);
        }

        ConversionAttempt Convert(string value)
            => values.TryGetValue(value, out object? result)
                ? ConversionAttempt.Succeeded(result)
                : ConversionAttempt.Failed;

        return (Convert, fields.Select(static field => field.Name).ToImmutableArray());
    }

    private static Func<string, ConversionAttempt> CreateParsableConverter<T>()
        where T : IParsable<T>
        => static value => T.TryParse(value, CultureInfo.InvariantCulture, out T? result)
            ? result is null
                ? ConversionAttempt.NullSuccess
                : ConversionAttempt.Succeeded(result)
            : ConversionAttempt.Failed;

    private static ImmutableArray<T> CreateImmutableList<T>(IReadOnlyList<object?> values)
    {
        ImmutableArray<T>.Builder builder = ImmutableArray.CreateBuilder<T>(values.Count);
        foreach (object? value in values)
        {
            builder.Add((T)value!);
        }

        return builder.MoveToImmutable();
    }
    [StructLayout(LayoutKind.Auto)]
    internal readonly record struct ConversionAttempt(bool IsSuccess, bool IsNullSuccess, object? Value)
    {
        internal static ConversionAttempt Failed => new(false, false, null);

        internal static ConversionAttempt NullSuccess => new(false, true, null);

        internal static ConversionAttempt Succeeded(object value) => new(true, false, value);
    }
}

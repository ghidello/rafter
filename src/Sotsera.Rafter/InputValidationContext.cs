using Sotsera.Rafter.Internal;

namespace Sotsera.Rafter;

/// <summary>Prepared input values available to combined rules, without execution facilities.</summary>
public sealed class InputValidationContext
{
    private readonly InputValues _values;

    internal InputValidationContext(InputValues values)
    {
        _values = values;
    }

    /// <summary>Reads a prepared required or defaulted value.</summary>
    public T GetValue<T>(ValueOption<T> option) => _values.GetRequired(option);

    /// <summary>Reads an optional string; null means omitted.</summary>
    public string? GetValue(Option<string> option) => _values.GetString(option);

    /// <summary>Reads an optional value type; null means omitted.</summary>
    public T? GetValue<T>(Option<T> option) where T : struct => _values.GetOptional(option);
}

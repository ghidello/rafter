using System.Buffers;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace Sotsera.Rafter;

internal static class ValueFormatter
{
    private const int StringElementLimit = 64;

    internal static bool TryFormatStable(object value, Type declaredType, out string stable, out string display)
    {
        Type type = Nullable.GetUnderlyingType(declaredType) ?? declaredType;
        if (type == typeof(string))
        {
            stable = (string)value;
            display = FormatText(stable);
            return true;
        }

        if (type == typeof(char))
        {
            stable = ((char)value).ToString();
            display = FormatText(stable, truncate: false);
            return true;
        }

        if (type == typeof(bool))
        {
            stable = (bool)value ? "true" : "false";
            display = stable;
            return true;
        }

        if (type.IsEnum)
        {
            string? name = GetCanonicalEnumName(type, value);
            stable = name ?? string.Empty;
            display = stable;
            return name is not null;
        }

        string? format = GetInvariantFormat(type);
        if (format is null)
        {
            stable = string.Empty;
            display = string.Empty;
            return false;
        }

        stable = ((IFormattable)value).ToString(format, CultureInfo.InvariantCulture);
        if (type == typeof(Guid))
        {
            stable = stable.ToLowerInvariant();
        }

        display = stable;
        return true;
    }

    internal static string FormatText(string value, bool truncate = true)
    {
        StringBuilder builder = new(value.Length + 2);
        builder.Append('"');
        ReadOnlySpan<char> remaining = value.AsSpan();
        int elements = 0;
        bool wasTruncated = false;

        while (!remaining.IsEmpty)
        {
            if (truncate && elements == StringElementLimit)
            {
                wasTruncated = true;
                break;
            }

            OperationStatus status = Rune.DecodeFromUtf16(remaining, out Rune rune, out int consumed);
            if (status == OperationStatus.Done)
            {
                AppendEscaped(builder, rune);
                remaining = remaining[consumed..];
            }
            else
            {
                AppendHexEscape(builder, remaining[0]);
                remaining = remaining[1..];
            }

            elements++;
        }

        if (wasTruncated)
        {
            builder.Append("...");
        }

        builder.Append('"');
        return builder.ToString();
    }

    internal static string FormatInlineText(string value)
    {
        string quoted = FormatText(value);
        return quoted[1..^1];
    }

    private static void AppendEscaped(StringBuilder builder, Rune rune)
    {
        switch (rune.Value)
        {
            case '"':
                builder.Append("\\\"");
                return;
            case '\\':
                builder.Append("\\\\");
                return;
            case '\b':
                builder.Append("\\b");
                return;
            case '\t':
                builder.Append("\\t");
                return;
            case '\n':
                builder.Append("\\n");
                return;
            case '\f':
                builder.Append("\\f");
                return;
            case '\r':
                builder.Append("\\r");
                return;
        }

        UnicodeCategory category = Rune.GetUnicodeCategory(rune);
        if (category is UnicodeCategory.Control
            or UnicodeCategory.Format
            or UnicodeCategory.LineSeparator
            or UnicodeCategory.ParagraphSeparator)
        {
            AppendHexEscape(builder, rune.Value);
            return;
        }

        builder.Append(rune.ToString());
    }

    private static void AppendHexEscape(StringBuilder builder, int value)
    {
        if (value <= char.MaxValue)
        {
            builder.Append("\\u");
            builder.Append(value.ToString("X4", CultureInfo.InvariantCulture));
            return;
        }

        builder.Append("\\U");
        builder.Append(value.ToString("X8", CultureInfo.InvariantCulture));
    }

    private static string? GetCanonicalEnumName(Type type, object value)
    {
        foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Static).OrderBy(static field => field.MetadataToken))
        {
            if (Equals(field.GetValue(null), value))
            {
                return field.Name;
            }
        }

        return null;
    }

    private static string? GetInvariantFormat(Type type)
    {
        if (type == typeof(sbyte)
            || type == typeof(byte)
            || type == typeof(short)
            || type == typeof(ushort)
            || type == typeof(int)
            || type == typeof(uint)
            || type == typeof(long)
            || type == typeof(ulong)
            || type == typeof(Int128)
            || type == typeof(UInt128)
            || type == typeof(nint)
            || type == typeof(nuint))
        {
            return "D";
        }

        if (type == typeof(Half) || type == typeof(float) || type == typeof(double))
        {
            return "R";
        }

        if (type == typeof(decimal))
        {
            return "G29";
        }

        if (type == typeof(Guid))
        {
            return "D";
        }

        if (type == typeof(DateTime) || type == typeof(DateTimeOffset) || type == typeof(DateOnly) || type == typeof(TimeOnly))
        {
            return "O";
        }

        return type == typeof(TimeSpan) ? "c" : null;
    }
}

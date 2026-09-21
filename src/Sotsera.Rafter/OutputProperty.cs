using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Sotsera.Rafter;

internal sealed record OutputProperty(string Name, string CanonicalValue)
{
    internal static string Quote(string value)
    {
        StringBuilder result = new(value.Length + 2);
        result.Append('"');
        foreach (char character in value)
        {
            result.Append(character switch
            {
                '"' => "\\\"",
                '\\' => "\\\\",
                '\b' => "\\b",
                '\f' => "\\f",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                _ when char.IsControl(character) || char.IsSurrogate(character)
                    => "\\u" + ((int)character).ToString("x4", CultureInfo.InvariantCulture),
                _ => character.ToString(),
            });
        }
        return result.Append('"').ToString();
    }

    internal OutputProperty Redact(TextRedactor redactor)
    {
        using JsonDocument document = JsonDocument.Parse(CanonicalValue);
        return new OutputProperty(RedactText(Name, redactor), RedactValue(document.RootElement, redactor));
    }

    internal static string DecodeString(JsonElement value)
    {
        // Our canonical JSON escapes every surrogate code unit. GetString rejects unpaired surrogates,
        // but property snapshots must preserve the original UTF-16 for redaction and visible escaping.
        string literal = value.GetRawText();
        StringBuilder decoded = new(literal.Length - 2);
        for (int index = 1; index < literal.Length - 1; index++)
        {
            char character = literal[index];
            if (character != '\\')
            {
                decoded.Append(character);
                continue;
            }

            character = literal[++index];
            if (character == 'u')
            {
                decoded.Append((char)int.Parse(literal.AsSpan(index + 1, 4), NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture));
                index += 4;
                continue;
            }

            decoded.Append(character switch
            {
                '"' or '\\' or '/' => character,
                'b' => '\b',
                'f' => '\f',
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                _ => throw new InvalidOperationException("Invalid canonical property escape."),
            });
        }
        return decoded.ToString();
    }

    private static string RedactValue(JsonElement value, TextRedactor redactor)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            return "[" + string.Join(',', value.EnumerateArray().Select(item => RedactValue(item, redactor))) + "]";
        }
        if (value.ValueKind == JsonValueKind.String)
        {
            return Quote(RedactText(DecodeString(value), redactor));
        }
        string original = value.GetRawText();
        string safe = RedactText(original, redactor);
        return string.Equals(original, safe, StringComparison.Ordinal) ? original : Quote(safe);
    }

    private static string RedactText(string text, TextRedactor redactor)
        => redactor.TryRedact(text, out string safe)
            ? safe
            : throw new InvalidOperationException("A property could not be redacted safely.");
}

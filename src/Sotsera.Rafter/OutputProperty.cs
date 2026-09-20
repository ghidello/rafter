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

    private static string RedactValue(JsonElement value, TextRedactor redactor)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            return "[" + string.Join(',', value.EnumerateArray().Select(item => RedactValue(item, redactor))) + "]";
        }
        if (value.ValueKind == JsonValueKind.String)
        {
            return Quote(RedactText(value.GetString()!, redactor));
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

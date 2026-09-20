using System.Globalization;
using System.Text;

namespace Sotsera.Rafter.Tests;

// Models the cursor operations emitted by the live renderer, preserving SGR for golden documents.
internal sealed class TerminalSurfaceWriter : StringWriter
{
    private readonly List<StringBuilder> _lines = [new()];
    private int _row;

    public override void Write(string? value)
    {
        if (value is null)
        {
            return;
        }

        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (character == '\u001b')
            {
                int end = index + 2;
                while (end < value.Length && !char.IsAsciiLetter(value[end]))
                {
                    end++;
                }

                char command = value[end];
                if (command == 'm')
                {
                    _lines[_row].Append(value.AsSpan(index, end - index + 1));
                }
                else if (command == 'A')
                {
                    _row -= int.Parse(value.AsSpan(index + 2, end - index - 2), CultureInfo.InvariantCulture);
                    _row.Should().BeGreaterThanOrEqualTo(0);
                }
                else
                {
                    command.Should().Be('K');
                    value.AsSpan(index + 2, end - index - 2).ToString().Should().Be("2");
                    _lines[_row].Clear();
                }
                index = end;
            }
            else if (character == '\n')
            {
                _row++;
                if (_lines.Count == _row)
                {
                    _lines.Add(new StringBuilder());
                }
            }
            else if (character != '\r')
            {
                _lines[_row].Append(character);
            }
        }
    }

    public override string ToString()
        => string.Join('\n', _lines.Take(_row + 1).Select(line => line.ToString()));
}

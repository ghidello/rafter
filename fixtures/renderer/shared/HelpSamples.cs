using Spectre.Console;

namespace Rafter.Evaluation;

internal static class HelpSamples
{
    public static string[] Lines(string scenario, bool adversarial = false)
    {
        string[] controls =
        [
            "Rafter options", "  --help", "    Show help without executing work.",
            "  --quiet", "    Hide routine lifecycle output; keep results and errors.",
            "  --verbose", "    Add execution context. Cannot combine with --quiet.",
            "  --plain", "    Disable Rafter styling and presentation prefixes."
        ];
        string[] lines = scenario switch
        {
            "options" =>
            [
                "Print a personalized greeting.", "", "Command options",
                "  --name, -n, --recipient <string>", "    The person to greet. Required.",
                "  --repeat, -r <int>", "    Number of greetings. Default: 1.",
                "  --uppercase, -u [bool]", "    Use uppercase text. Default: false.",
                "    Bare flag means true; explicit true/false allowed.", "", "Execution",
                "  Runs the supplied command body after input validation.", "", .. controls
            ],
            "validation" =>
            [
                "Describe a range after validating its bounds and optional label.", "", "Command options",
                "  --minimum <int>", "    The inclusive lower bound. Default: 1.", "    Rules:",
                "      - Must be zero or greater.", "      - Must not exceed one hundred.", "",
                "  --maximum <int>", "    The inclusive upper bound. Default: 10.", "    Rules:",
                "      - Must be zero or greater.", "      - Must not exceed one hundred.", "",
                "  --label <string>", "    An optional name for the range. Optional; no default.",
                "    Rules when supplied:", "      - Must not be blank when supplied.", "",
                "Execution", "  Runs the supplied command body after input validation.",
                "  Combined input rules:", "    - Minimum must not exceed maximum.", "", .. controls
            ],
            "dependencies" =>
            [
                "Build the project and run its tests.", "", "Command options", "  None declared.", "",
                "Execution", "  The script selects verify. Each target body runs once.", "",
                "  > verify <-- (build, test)", "    Build and test the project. (Group; no body.)", "",
                "  [build]", "    Build the project.", "", "  [test] <-- (build)",
                "    Run the tests after a successful build.", "", "  Body order: build, test.", "", .. controls
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };

        if (adversarial)
        {
            return [.. lines, "", "  --very-long-alias-" + new string('x', 70) + ", -z <string>",
                "    Literal [red]brackets[/], cafe\u0301, 界面, and 👩‍💻 remain visible."];
        }

        return lines;
    }

    public static void Write(RendererDestination destination, string scenario, bool adversarial = false)
    {
        var lines = Lines(scenario, adversarial);
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index];
            var indentation = line.TakeWhile(character => character == ' ').Count();
            var content = line[indentation..];
            var style = index == 0 ? new Style(decoration: Decoration.Bold) : Style.Plain;
            if (line is "Command options" or "Execution" or "Rafter options")
            {
                style = new Style(new Color(154, 214, 193), decoration: Decoration.Bold);
            }

            var paragraph = new Paragraph { Overflow = Overflow.Fold };
            if (content.StartsWith("--", StringComparison.Ordinal))
            {
                var typeStart = content.IndexOfAny(['<', '[']);
                var names = typeStart < 0 ? content : content[..typeStart];
                var aliases = names.Split(", ", StringSplitOptions.None);
                for (var alias = 0; alias < aliases.Length; alias++)
                {
                    if (alias > 0)
                    {
                        paragraph.Append(", ", new Style(Color.Grey));
                    }

                    paragraph.Append(aliases[alias], new Style(new Color(173, 214, 240)));
                }

                if (typeStart >= 0)
                {
                    paragraph.Append(content[typeStart..], new Style(new Color(223, 196, 255)));
                }
            }
            else
            {
                paragraph.Append(content, style);
            }

            destination.Layout(paragraph, indentation);
        }
    }
}

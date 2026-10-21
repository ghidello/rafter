using System.Text;
using System.Text.Json;
using Spectre.Console;

namespace Rafter.Evaluation;

internal static class RendererProbe
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Contains("--measure"))
        {
            return BenchmarkProbe.Run(() =>
            {
                var destination = new RendererDestination(TextWriter.Null, new(true, false));
                return () => destination.Framework(BenchmarkProbe.Message, new Style(Color.Yellow));
            }, args.Contains("--steady"));
        }

        var stdout = Console.Out;
        var stderr = Console.Error;
        var plain = args.Contains("--plain");
        var noColor = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));
        var outputPolicy = DestinationPolicy.Detect(stdout, Console.IsOutputRedirected, plain, noColor);
        var errorPolicy = DestinationPolicy.Detect(stderr, Console.IsErrorRedirected, plain, noColor);
        var outputRecorder = new RecordingWriter(stdout);
        var errorRecorder = new RecordingWriter(stderr);
        var width = args.Contains("--narrow") ? 48 : 100;
        var output = new RendererDestination(outputRecorder, outputPolicy, width);
        var error = new RendererDestination(errorRecorder, errorPolicy, width);

        var helpIndex = Array.IndexOf(args, "--help-sample");
        if (helpIndex >= 0)
        {
            HelpSamples.Write(output, args[helpIndex + 1], args.Contains("--adversarial"));
        }
        else
        {
            output.Literal("  script [red]literal[/]\n\n", "  | ");
            error.Framework("Cancellation requested.", new Style(Color.Yellow));
            output.Literal("child stdout\rprogress\npartial");
            error.Literal("  child stderr\n");
        }

        await Task.WhenAll(output.CompleteAsync(), error.CompleteAsync());
        var recordIndex = Array.IndexOf(args, "--record");
        if (recordIndex >= 0)
        {
            await File.WriteAllTextAsync(args[recordIndex + 1], JsonSerializer.Serialize(new
            {
                OutputRedirected = Console.IsOutputRedirected,
                ErrorRedirected = Console.IsErrorRedirected,
                OutputRich = outputPolicy.Rich, ErrorRich = errorPolicy.Rich,
                Plain = plain, NoColor = noColor, Width = width,
                Output = outputRecorder.Text, Error = errorRecorder.Text,
                WritersRetained = ReferenceEquals(stdout, Console.Out) && ReferenceEquals(stderr, Console.Error),
                OutputEncoding = stdout.Encoding.WebName, ErrorEncoding = stderr.Encoding.WebName,
                OS = Environment.OSVersion.VersionString,
                Framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                Term = Environment.GetEnvironmentVariable("TERM"),
                GithubActions = Environment.GetEnvironmentVariable("GITHUB_ACTIONS")
            }));
        }

        return 0;
    }

    private sealed class RecordingWriter(TextWriter destination) : TextWriter
    {
        private readonly StringBuilder _text = new();

        public override Encoding Encoding => destination.Encoding;
        public string Text => _text.ToString();

        public override void Write(string? value)
        {
            _text.Append(value);
            destination.Write(value);
        }

        public override void Write(char value)
        {
            _text.Append(value);
            destination.Write(value);
        }

        public override Task FlushAsync() => destination.FlushAsync();
    }
}

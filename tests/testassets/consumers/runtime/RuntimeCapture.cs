using System.Text;
using System.Text.Json;

// Review-only tee: preserve the real OS handles and record text before the terminal interprets ANSI.
internal static class RuntimeCapture
{
    public static async Task<int> RunAsync(string[] args)
    {
        var path = Environment.GetEnvironmentVariable("RAFTER_CAPTURE");
        if (string.IsNullOrEmpty(path))
        {
            return await Scenario.RunAsync(args);
        }

        var output = Console.Out;
        var error = Console.Error;
        var recordedOutput = new RecordingWriter(output);
        var recordedError = new RecordingWriter(error);
        Console.SetOut(recordedOutput);
        Console.SetError(recordedError);
        try
        {
            var result = await Scenario.RunAsync(args);
            await Console.Out.FlushAsync();
            await Console.Error.FlushAsync();
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new
            {
                Arguments = args, ExitCode = result,
                OutputRedirected = Console.IsOutputRedirected, ErrorRedirected = Console.IsErrorRedirected,
                Width = Console.IsOutputRedirected && Console.IsErrorRedirected ? 80 : Console.WindowWidth,
                NoColor = Environment.GetEnvironmentVariable("NO_COLOR"),
                Output = recordedOutput.Text, Error = recordedError.Text,
                OS = Environment.OSVersion.VersionString,
                Framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription
            }));
            return result;
        }
        finally
        {
            Console.SetOut(output);
            Console.SetError(error);
        }
    }

    private sealed class RecordingWriter(TextWriter destination) : TextWriter
    {
        private readonly StringBuilder _text = new();

        public override Encoding Encoding => destination.Encoding;
        public string Text => _text.ToString();

        public override void Write(char value)
        {
            _text.Append(value);
            destination.Write(value);
        }

        public override void Write(string? value)
        {
            _text.Append(value);
            destination.Write(value);
        }

        public override void Write(ReadOnlySpan<char> value)
        {
            _text.Append(value);
            destination.Write(value);
        }

        public override void Write(char[] buffer, int index, int count)
        {
            _text.Append(buffer, index, count);
            destination.Write(buffer, index, count);
        }

        public override Task FlushAsync() => destination.FlushAsync();
    }
}

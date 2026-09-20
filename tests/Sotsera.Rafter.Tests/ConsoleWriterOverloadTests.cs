using System.Globalization;
using System.Text;

namespace Sotsera.Rafter.Tests;

[Collection(ConsoleOutputCoordinatorFixture.Name)]
public sealed class ConsoleWriterOverloadTests
{
    private static readonly char[] Characters = ['x'];
    private static readonly char[] SegmentCharacters = ['a', 'x', 'b'];

    public static TheoryData<string, bool, bool> LineCases
    {
        get
        {
            TheoryData<string, bool, bool> cases = new();
            string[] operations = [
                "empty", "char", "array", "segment", "span", "string", "builder", "object", "bool",
                "int", "uint", "long", "ulong", "float", "double", "decimal",
                "format-one", "format-two", "format-three", "format-array", "format-span",
                "async-empty", "async-char", "async-string", "async-array", "async-segment", "async-memory",
                "async-builder", "null-string", "null-array", "null-builder", "null-object", "null-format-result",
            ];
            foreach (string operation in operations)
            {
                foreach (bool managed in new[] { false, true })
                {
                    cases.Add(operation, managed, false);
                    cases.Add(operation, managed, true);
                }
            }
            return cases;
        }
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task BuilderChunksReachTheHostInOneSynchronousWrite(bool asynchronous, bool line, bool standardError)
    {
        TextWriter original = standardError ? Console.Error : Console.Out;
        RecordingWriter host = new();
        StringWriter sink = new();
        InvocationOutput output = new(sink, sink, OutputCapabilities.Plain, OutputCapabilities.Plain,
            TextRedactor.Empty);
        StringBuilder builder = new(1);
        builder.Append('a', 128).Append('b', 256);
        try
        {
            SetHost(standardError, host);
            using ConsoleOutputCoordinator.Lease lease = ConsoleOutputCoordinator.Register(output);
            TextWriter writer = standardError ? Console.Error : Console.Out;
            writer.NewLine = "~";
            Task hostWrite;
            using (ExecutionContext.SuppressFlow())
            {
                hostWrite = Task.Run(async () =>
                {
                    if (asynchronous)
                    {
                        Task write = line
                            ? writer.WriteLineAsync(builder, TestContext.Current.CancellationToken)
                            : writer.WriteAsync(builder, TestContext.Current.CancellationToken);
                        write.IsCompletedSuccessfully.Should().BeTrue();
                        await write.ConfigureAwait(false);
                    }
                    else if (line)
                    {
                        writer.WriteLine(builder);
                    }
                    else
                    {
                        writer.Write(builder);
                    }
                }, TestContext.Current.CancellationToken);
            }
            await hostWrite.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

            host.Writes.Should().Be(1);
            host.ToString().Should().Be(builder.ToString() + (line ? "~" : string.Empty));
            sink.ToString().Should().BeEmpty();
        }
        finally
        {
            SetHost(standardError, original);
        }
    }

    [Theory]
    [InlineData("memory", false)]
    [InlineData("memory", true)]
    [InlineData("builder", false)]
    [InlineData("builder", true)]
    [InlineData("line-memory", false)]
    [InlineData("line-memory", true)]
    [InlineData("line-builder", false)]
    [InlineData("line-builder", true)]
    public async Task PrecancelledWritesDoNotPublishOrChangeInvocationHealth(string operation, bool standardError)
    {
        StringWriter sink = new();
        InvocationOutput output = new(sink, sink, OutputCapabilities.Plain, OutputCapabilities.Plain,
            TextRedactor.Empty);
        using ConsoleOutputCoordinator.Lease lease = ConsoleOutputCoordinator.Register(output);
        TextWriter writer = standardError ? Console.Error : Console.Out;
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        CancellationToken token = cancellation.Token;
        Func<Task> write = operation switch
        {
            "memory" => () => writer.WriteAsync("cancelled".AsMemory(), token),
            "builder" => () => writer.WriteAsync(new StringBuilder("cancelled"), token),
            "line-memory" => () => writer.WriteLineAsync("cancelled".AsMemory(), token),
            "line-builder" => () => writer.WriteLineAsync(new StringBuilder("cancelled"), token),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

        (await write.Should().ThrowAsync<OperationCanceledException>()).Which.CancellationToken.Should().Be(token);
        writer.WriteLine("after cancellation");
        await output.SealAsync();

        output.Failure.Should().BeNull();
        sink.ToString().Should().Be("[command] after cancellation\n");
    }

    [Theory]
    [MemberData(nameof(LineCases))]
    public async Task LineOverloadsHonorChangedNewlinesAndPublishSynchronously(
        string operation, bool managed, bool standardError)
    {
        TextWriter original = standardError ? Console.Error : Console.Out;
        RecordingWriter host = new();
        StringWriter sink = new(CultureInfo.InvariantCulture);
        InvocationOutput output = new(sink, sink, OutputCapabilities.Plain, OutputCapabilities.Plain,
            TextRedactor.Empty);
        try
        {
            SetHost(standardError, host);
            TextWriter captured = standardError ? Console.Error : Console.Out;
            using (ConsoleOutputCoordinator.Lease lease = ConsoleOutputCoordinator.Register(output))
            {
                TextWriter writer = standardError ? Console.Error : Console.Out;
                if (managed)
                {
                    await WriteLinesAsync(writer, operation);
                }
                else
                {
                    Task hostWrite;
                    using (ExecutionContext.SuppressFlow())
                    {
                        hostWrite = Task.Run(() => WriteLinesAsync(writer, operation),
                            TestContext.Current.CancellationToken);
                    }
                    await hostWrite.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
                }
                await output.SealAsync();
                writer.NewLine.Should().Be("~");
            }

            StringWriter expected = new(CultureInfo.InvariantCulture);
            await WriteLinesAsync(expected, operation);
            string text = expected.ToString();
            if (managed)
            {
                sink.ToString().Should().Be($"[command] {text}\n");
                host.ToString().Should().BeEmpty();
            }
            else
            {
                host.ToString().Should().Be(text);
                host.Writes.Should().Be(2, "each line overload must reach the host as one complete write");
                sink.ToString().Should().BeEmpty();
            }
            output.Failure.Should().BeNull();
            captured.NewLine.Should().Be("~");
            (standardError ? Console.Error : Console.Out).Should().BeSameAs(captured);
        }
        finally
        {
            SetHost(standardError, original);
        }
    }

    private static async Task WriteLinesAsync(TextWriter writer, string operation)
    {
        foreach (string newline in new[] { "|", "~" })
        {
            writer.NewLine = newline;
            Task write = WriteLineAsync(writer, operation);
            write.IsCompletedSuccessfully.Should().BeTrue();
            await write.ConfigureAwait(false);
        }
    }

    private static Task WriteLineAsync(TextWriter writer, string operation)
    {
        if (operation.StartsWith("async-", StringComparison.Ordinal))
        {
            return operation switch
            {
                "async-empty" => writer.WriteLineAsync(),
                "async-char" => writer.WriteLineAsync('x'),
                "async-string" => writer.WriteLineAsync("x"),
                "async-array" => writer.WriteLineAsync(Characters),
                "async-segment" => writer.WriteLineAsync(SegmentCharacters, 1, 1),
                "async-memory" => writer.WriteLineAsync("x".AsMemory(), TestContext.Current.CancellationToken),
                "async-builder" => writer.WriteLineAsync(new StringBuilder("x"), TestContext.Current.CancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(operation)),
            };
        }

        Action write = operation switch
        {
            "empty" => () => writer.WriteLine(),
            "char" => () => writer.WriteLine('x'),
            "array" => () => writer.WriteLine(Characters),
            "segment" => () => writer.WriteLine(SegmentCharacters, 1, 1),
            "span" => () => writer.WriteLine("x".AsSpan()),
            "string" => () => writer.WriteLine("x"),
            "null-string" => () => writer.WriteLine((string?)null),
            "null-array" => () => writer.WriteLine((char[]?)null),
            "null-builder" => () => writer.WriteLine((StringBuilder?)null),
            "null-object" => () => writer.WriteLine((object?)null),
            "null-format-result" => () => writer.WriteLine(new NullFormattable()),
            "builder" => () => writer.WriteLine(new StringBuilder("x")),
            "object" => () => writer.WriteLine((object)"x"),
            "bool" => () => writer.WriteLine(true),
            "int" => () => writer.WriteLine(42),
            "uint" => () => writer.WriteLine(42U),
            "long" => () => writer.WriteLine(42L),
            "ulong" => () => writer.WriteLine(42UL),
            "float" => () => writer.WriteLine(42F),
            "double" => () => writer.WriteLine(42D),
            "decimal" => () => writer.WriteLine(42M),
            "format-one" => () => writer.WriteLine("{0}", "x"),
            "format-two" => () => writer.WriteLine("{0}{1}", "x", "y"),
            "format-three" => () => writer.WriteLine("{0}{1}{2}", "x", "y", "z"),
            "format-array" => () => writer.WriteLine("{0}", new object[] { "x" }),
            "format-span" => () => writer.WriteLine("{0}", new ReadOnlySpan<object?>(["x"])),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };
        write();
        return Task.CompletedTask;
    }

    private static void SetHost(bool standardError, TextWriter writer)
    {
        if (standardError)
        {
            Console.SetError(writer);
        }
        else
        {
            Console.SetOut(writer);
        }
    }

    private sealed class RecordingWriter : StringWriter
    {
        internal int Writes { get; private set; }

        public override void Write(string? value)
        {
            Writes++;
            base.Write(value);
        }
    }

    private sealed class NullFormattable : IFormattable
    {
        public string ToString(string? format, IFormatProvider? formatProvider) => null!;
    }
}

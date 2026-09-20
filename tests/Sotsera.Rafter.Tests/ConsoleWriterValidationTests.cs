namespace Sotsera.Rafter.Tests;

[Collection(ConsoleOutputCoordinatorFixture.Name)]
public sealed class ConsoleWriterValidationTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task InvalidSegmentsMatchTextWriterValidationWithoutPublishing(bool line, bool standardError)
    {
        StringWriter sink = new();
        InvocationOutput output = new(sink, sink, OutputCapabilities.Plain, OutputCapabilities.Plain,
            TextRedactor.Empty);
        using ConsoleOutputCoordinator.Lease lease = ConsoleOutputCoordinator.Register(output);
        TextWriter writer = standardError ? Console.Error : Console.Out;
        char[] characters = ['x'];
        (char[]? Buffer, int Index, int Count)[] invalid = [
            (null, 0, 0), (characters, -1, 1), (characters, 0, -1),
            (characters, 2, 0), (characters, 1, 1), (characters, int.MaxValue, int.MaxValue),
        ];
        foreach ((char[]? buffer, int index, int count) in invalid)
        {
            foreach (bool asynchronous in new[] { false, true })
            {
                Func<Task> expectedWrite = () => WriteSegmentAsync(new StringWriter(), buffer!, (index, count),
                    (line, asynchronous));
                Func<Task> actualWrite = () => WriteSegmentAsync(writer, buffer!, (index, count), (line, asynchronous));
                ArgumentException expected = (await expectedWrite.Should().ThrowAsync<ArgumentException>()).Which;
                ArgumentException actual = (await actualWrite.Should().ThrowAsync<ArgumentException>()).Which;
                actual.GetType().Should().Be(expected.GetType());
                actual.ParamName.Should().Be(expected.ParamName);
            }
        }
        await output.SealAsync();

        sink.ToString().Should().BeEmpty();
        output.Failure.Should().BeNull();
    }

    private static Task WriteSegmentAsync(
        TextWriter writer, char[] buffer, (int Index, int Count) segment, (bool Line, bool Async) operation)
    {
        if (operation.Async)
        {
            return operation.Line
                ? writer.WriteLineAsync(buffer, segment.Index, segment.Count)
                : writer.WriteAsync(buffer, segment.Index, segment.Count);
        }
        if (operation.Line)
        {
            writer.WriteLine(buffer, segment.Index, segment.Count);
        }
        else
        {
            writer.Write(buffer, segment.Index, segment.Count);
        }
        return Task.CompletedTask;
    }
}

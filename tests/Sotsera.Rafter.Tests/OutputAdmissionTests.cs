using System.Collections;
using System.Diagnostics;

namespace Sotsera.Rafter.Tests;

public sealed class OutputAdmissionTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public async Task SealingWaitsForAdmittedUserCodeAndPreservesItsFailure(
        bool collection, bool priorOutputFailure, bool callerFailure)
    {
        StringWriter sink = new();
        InvocationOutput invocation = CreateOutput(sink);
        RafterOutput output = new(invocation, new OutputScope("work"));
        IOException sinkFailure = new("earlier sink failure");
        InvalidDataException formattingFailure = new("caller formatting failure");
        Task? closing = null;
        int userCalls = 0;
        if (priorOutputFailure)
        {
            invocation.Fail(sinkFailure);
        }

        CallbackScalar scalar = new(() =>
        {
            userCalls++;
            closing = invocation.SealAsync();
            closing.IsCompleted.Should().BeFalse("this call still owns its admission while running caller code");
            Action late = () => output.Property("late", new CallbackScalar(() => throw new UnreachableException()));
            late.Should().Throw<InvalidOperationException>();
            if (callerFailure)
            {
                throw formattingFailure;
            }
            return "value";
        });
        Action publish = () => output.Property("item", collection ? new CallbackCollection(scalar) : scalar);

        if (callerFailure)
        {
            publish.Should().Throw<InvalidDataException>().Which.Should().BeSameAs(formattingFailure);
        }
        else
        {
            publish();
        }
        closing.Should().NotBeNull();
        await closing!.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        userCalls.Should().Be(1);
        invocation.Failure.Should().BeSameAs(priorOutputFailure ? sinkFailure : null);
        string literal = collection ? "[\"value\"]" : "\"value\"";
        sink.ToString().Should().Be(priorOutputFailure || callerFailure ? string.Empty : $"[work] item={literal}\n");
    }

    [Theory]
    [InlineData("line")]
    [InlineData("success")]
    [InlineData("warning")]
    [InlineData("error")]
    [InlineData("property")]
    public async Task SealedFacadeRejectsCallsBeforeValidationOrUserCode(string operation)
    {
        StringWriter sink = new();
        InvocationOutput invocation = CreateOutput(sink);
        RafterOutput output = new(invocation, new OutputScope("work"));
        await invocation.SealAsync();
        Action publish = operation switch
        {
            "line" => () => output.Line(null!),
            "success" => () => output.Success(string.Empty),
            "warning" => () => output.Warning(string.Empty),
            "error" => () => output.Error(string.Empty, "invalid\nrecovery"),
            "property" => () => output.Property("item", new CallbackCollection(
                new CallbackScalar(() => throw new UnreachableException()))),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

        publish.Should().Throw<InvalidOperationException>();
        sink.ToString().Should().BeEmpty();
        invocation.Failure.Should().BeNull();
    }

    [Fact]
    public async Task ConcurrentFacadeCallsEitherPublishBeforeSealingOrRejectWithoutFormatting()
    {
        StringWriter sink = new();
        InvocationOutput invocation = CreateOutput(sink);
        RafterOutput output = new(invocation, new OutputScope("work"));
        TaskCompletionSource start = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int formatted = 0;
        int accepted = 0;
        int rejected = 0;
        Task[] calls = Enumerable.Range(0, 64).Select(_ => Task.Run(async () =>
        {
            await start.Task.ConfigureAwait(false);
            try
            {
                output.Property("item", new CallbackScalar(() =>
                {
                    Interlocked.Increment(ref formatted);
                    return "value";
                }));
                Interlocked.Increment(ref accepted);
            }
            catch (InvalidOperationException)
            {
                Interlocked.Increment(ref rejected);
            }
        }, TestContext.Current.CancellationToken)).ToArray();

        Task? closing = null;
        output.Property("item", new CallbackScalar(() =>
        {
            Interlocked.Increment(ref formatted);
            start.SetResult();
            closing = invocation.SealAsync();
            closing.IsCompleted.Should().BeFalse();
            return "value";
        }));
        Interlocked.Increment(ref accepted);
        await closing!.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        string sealedText = sink.ToString();
        await Task.WhenAll(calls).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        (accepted + rejected).Should().Be(65);
        formatted.Should().Be(accepted);
        sealedText.Should().Be(string.Concat(Enumerable.Repeat("[work] item=\"value\"\n", accepted)));
        sink.ToString().Should().Be(sealedText, "no call may publish after sealing completes");
        invocation.Failure.Should().BeNull();
    }

    private static InvocationOutput CreateOutput(TextWriter sink)
        => new(sink, sink, OutputCapabilities.Plain, OutputCapabilities.Plain, TextRedactor.Empty);

    private sealed class CallbackScalar(Func<string> format)
    {
        public override string ToString() => format();
    }

    private sealed class CallbackCollection(CallbackScalar scalar) : IEnumerable
    {
        public IEnumerator GetEnumerator()
        {
            yield return scalar;
        }
    }
}

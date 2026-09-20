using System.Text;

namespace Sotsera.Rafter.Tests;

public sealed class ProcessCaptureMemoryTests
{
    [Theory]
    [InlineData(16_383)]
    [InlineData(16_384)]
    [InlineData(16_385)]
    [InlineData(1_048_576)]
    [InlineData(8_388_608)]
    public void CaptureAllocationsFollowTheSegmentAndStringEnvelope(int bytesPerStream)
    {
        byte[] chunk = new byte[997];
        Array.Fill(chunk, (byte)'a');
        // Warm the synchronous buffer path before measuring allocations on this thread.
        _ = CapturePair(32, chunk);
        long before = GC.GetAllocatedBytesForCurrentThread();
        (string stdout, string stderr, long capacity) = CapturePair(bytesPerStream, chunk);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        long segments = (bytesPerStream + 16_383L) / 16_384;
        long maximum = 6L * bytesPerStream + 2 * 16_384 + 256 * segments + 16_384;

        stdout.Length.Should().Be(bytesPerStream);
        stderr.Should().Be(stdout);
        capacity.Should().Be(2 * segments * 16_384);
        allocated.Should().BeLessThanOrEqualTo(maximum,
            "two bounded byte buffers plus two UTF-16 results need no intermediate full-size copy");
        TestContext.Current.TestOutputHelper!.WriteLine(
            $"bytes/stream={bytesPerStream}; segment-capacity={capacity}; allocated={allocated}; bound={maximum}");
    }

    [Theory]
    [InlineData(16_383)]
    [InlineData(16_384)]
    [InlineData(16_385)]
    public void OverflowDropsAllRetainedSegments(int limit)
    {
        ProcessCaptureBuffer buffer = new(limit);
        buffer.TryAppend(new byte[limit]).Should().BeTrue();
        buffer.RetainedBytes.Should().Be(limit);
        buffer.TryAppend([1]).Should().BeFalse();
        buffer.RetainedBytes.Should().Be(0);
        buffer.CapacityBytes.Should().Be(0);
    }

    [Theory]
    [InlineData("€")]
    [InlineData("😀")]
    public void MaterializationPreservesRunesAcrossSegmentBoundariesAndReleasesBytes(string rune)
    {
        string expected = new string('a', 16_383) + rune;
        byte[] bytes = Encoding.UTF8.GetBytes(expected);
        ProcessCaptureBuffer buffer = new(bytes.Length);
        foreach (byte value in bytes)
        {
            buffer.TryAppend([value]).Should().BeTrue();
        }

        buffer.Materialize().Should().Be(expected);
        buffer.RetainedBytes.Should().Be(0);
        buffer.CapacityBytes.Should().Be(0);
    }

    [Fact]
    public void FailedMaterializationReleasesBytes()
    {
        ProcessCaptureBuffer buffer = new(1);
        buffer.TryAppend([0xff]).Should().BeTrue();
        Action materialize = () => buffer.Materialize();

        materialize.Should().Throw<DecoderFallbackException>();
        buffer.CapacityBytes.Should().Be(0);
    }

    private static (string Stdout, string Stderr, long Capacity) CapturePair(int length, byte[] chunk)
    {
        ProcessCaptureBuffer stdout = new(length);
        ProcessCaptureBuffer stderr = new(length);
        for (int offset = 0; offset < length; offset += chunk.Length)
        {
            ReadOnlySpan<byte> bytes = chunk.AsSpan(0, Math.Min(chunk.Length, length - offset));
            if (!stdout.TryAppend(bytes) || !stderr.TryAppend(bytes))
            {
                throw new InvalidOperationException("The accepted capture limit was exceeded.");
            }
        }
        long capacity = stdout.CapacityBytes + stderr.CapacityBytes;
        return (stdout.Materialize(), stderr.Materialize(), capacity);
    }
}

using System.Text;

namespace Sotsera.Rafter;

internal sealed class ProcessCaptureBuffer(long limitBytes)
{
    private const int SegmentSize = 16 * 1024;
    private readonly List<Segment> _segments = [];
    private long _retainedBytes;

    internal long RetainedBytes => _retainedBytes;

    internal long CapacityBytes => (long)_segments.Count * SegmentSize;

    internal bool TryAppend(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > limitBytes - _retainedBytes)
        {
            Clear();
            return false;
        }
        while (!bytes.IsEmpty)
        {
            if (_segments.Count == 0 || _segments[^1].Count == SegmentSize)
            {
                _segments.Add(new Segment());
            }
            Segment segment = _segments[^1];
            int copied = Math.Min(SegmentSize - segment.Count, bytes.Length);
            bytes[..copied].CopyTo(segment.Buffer.AsSpan(segment.Count));
            segment.Count += copied;
            _retainedBytes += copied;
            bytes = bytes[copied..];
        }
        return true;
    }

    internal string Materialize()
    {
        try
        {
            UTF8Encoding encoding = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
            Decoder counter = encoding.GetDecoder();
            int characterCount = 0;
            Span<char> scratch = stackalloc char[256];
            foreach (Segment segment in _segments)
            {
                ReadOnlySpan<byte> remaining = segment.Buffer.AsSpan(0, segment.Count);
                while (!remaining.IsEmpty)
                {
                    // GetCharCount does not advance the decoder's state across segment boundaries.
                    counter.Convert(remaining, scratch, flush: false, out int used, out int written, out _);
                    characterCount += written;
                    remaining = remaining[used..];
                }
            }
            counter.Convert([], scratch, flush: true, out _, out int finalCharacters, out _);
            characterCount += finalCharacters;
            return string.Create(characterCount, (encoding, _segments), static (characters, state) =>
            {
                Decoder decoder = state.encoding.GetDecoder();
                int offset = 0;
                foreach (Segment segment in state._segments)
                {
                    decoder.Convert(segment.Buffer.AsSpan(0, segment.Count), characters[offset..], flush: false,
                        out _, out int written, out _);
                    offset += written;
                }
                decoder.Convert([], characters[offset..], flush: true, out _, out _, out _);
            });
        }
        finally
        {
            // Returned text is independent of the execution-only byte segments, including on decode failure.
            Clear();
        }
    }

    internal void Clear()
    {
        _segments.Clear();
        _retainedBytes = 0;
    }

    private sealed class Segment
    {
        internal byte[] Buffer { get; } = new byte[SegmentSize];

        internal int Count { get; set; }
    }
}

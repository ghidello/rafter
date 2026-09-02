using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace Sotsera.Rafter;

internal sealed class TextRedactor
{
    private readonly ImmutableArray<string> _patterns;

    private TextRedactor(ImmutableArray<string> patterns, string? marker)
    {
        _patterns = patterns;
        Marker = marker;
    }

    internal static TextRedactor Empty { get; } = new([], "<redacted>");

    internal bool IsUsable => Marker is not null;

    internal string? Marker { get; }

    internal static TextRedactor Create(ImmutableArray<string> patterns)
    {
        ImmutableArray<string> distinct = patterns
            .Where(static pattern => pattern.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(static pattern => pattern.Length)
            .ThenBy(static pattern => pattern, StringComparer.Ordinal)
            .ToImmutableArray();
        return new TextRedactor(distinct, SelectMarker(distinct));
    }

    internal bool TryRedact(string text, out string redacted)
    {
        if (Marker is null)
        {
            redacted = string.Empty;
            return false;
        }

        if (_patterns.IsEmpty)
        {
            redacted = text;
            return true;
        }

        List<MatchInterval> intervals = FindIntervals(text);
        redacted = intervals.Count == 0 ? text : ReplaceIntervals(text, intervals);
        return !ContainsPattern(redacted);
    }

    internal bool ContainsPattern(string text)
        => _patterns.Any(pattern => text.Contains(pattern, StringComparison.Ordinal));

    private List<MatchInterval> FindIntervals(string text)
    {
        List<MatchInterval> intervals = [];
        foreach (string pattern in _patterns)
        {
            int start = 0;
            while (start <= text.Length - pattern.Length)
            {
                int index = text.IndexOf(pattern, start, StringComparison.Ordinal);
                if (index < 0)
                {
                    break;
                }

                intervals.Add(new MatchInterval(index, index + pattern.Length));
                start = index + 1;
            }
        }

        intervals.Sort(static (left, right) => left.Start != right.Start
            ? left.Start.CompareTo(right.Start)
            : right.End.CompareTo(left.End));
        return intervals;
    }

    private string ReplaceIntervals(string text, List<MatchInterval> intervals)
    {
        StringBuilder builder = new(text.Length);
        int position = 0;
        int intervalIndex = 0;
        while (intervalIndex < intervals.Count)
        {
            MatchInterval current = intervals[intervalIndex];
            int end = current.End;
            intervalIndex++;
            while (intervalIndex < intervals.Count && intervals[intervalIndex].Start < end)
            {
                end = Math.Max(end, intervals[intervalIndex].End);
                intervalIndex++;
            }

            builder.Append(text, position, current.Start - position);
            builder.Append(Marker);
            position = end;
        }

        builder.Append(text, position, text.Length - position);
        return builder.ToString();
    }

    private static string? SelectMarker(ImmutableArray<string> patterns)
    {
        for (int index = 0; index < 10_000; index++)
        {
            string candidate = index == 0
                ? "<redacted>"
                : string.Create(CultureInfo.InvariantCulture, $"<redacted:{index}>");
            if (!patterns.Any(pattern => candidate.Contains(pattern, StringComparison.Ordinal)))
            {
                return candidate;
            }
        }

        for (int value = 0xE000; value <= 0xF8FF; value++)
        {
            string candidate = ((char)value).ToString();
            if (!patterns.Any(pattern => candidate.Contains(pattern, StringComparison.Ordinal)))
            {
                return candidate;
            }
        }

        return null;
    }

    internal sealed class Registry
    {
        private readonly HashSet<string> _patterns = new(StringComparer.Ordinal);

        internal void Add(string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                _patterns.Add(value);
            }
        }

        internal TextRedactor Freeze() => Create([.. _patterns]);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly record struct MatchInterval(int Start, int End);
}

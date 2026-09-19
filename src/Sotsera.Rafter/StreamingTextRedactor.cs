namespace Sotsera.Rafter;

internal sealed class StreamingTextRedactor
{
    private readonly string _marker;
    private readonly int _maximumPatternLength;
    private readonly int _unresolvedCharacters;
    private readonly string[] _patterns;
    private readonly LinkedList<PendingCharacter> _pending = [];
    private bool _insideRedaction;
    private bool _previousCarriageReturn;

    internal StreamingTextRedactor(TextRedactor redactor)
    {
        if (!redactor.IsUsable)
        {
            throw new InvalidOperationException("A streaming redactor requires a safe replacement marker.");
        }

        _marker = redactor.Marker!;
        _patterns = [.. redactor.Patterns.Select(NormalizeNewlines)];
        _maximumPatternLength = _patterns.Length == 0 ? 0 : _patterns.Max(static pattern => pattern.Length);
        _unresolvedCharacters = Math.Max(0, _maximumPatternLength - 1);
    }

    internal void Append(string scope, string text, Action<string, string> emit)
    {
        foreach (char character in text)
        {
            // Process drains enter before console normalization; normalize input and patterns identically.
            bool skipLineFeed = _previousCarriageReturn && character == '\n';
            _previousCarriageReturn = character == '\r';
            if (skipLineFeed)
            {
                continue;
            }

            _pending.AddLast(new PendingCharacter(scope, character == '\r' ? '\n' : character));
            MarkMatches();
            while (_pending.Count > _unresolvedCharacters)
            {
                EmitFirst(emit);
            }
        }
    }

    internal void Complete(Action<string, string> emit)
    {
        while (_pending.Count != 0)
        {
            EmitFirst(emit);
        }

        _insideRedaction = false;
        _previousCarriageReturn = false;
    }

    private void MarkMatches()
    {
        foreach (string pattern in _patterns)
        {
            LinkedListNode<PendingCharacter>? node = _pending.Last;
            int index = pattern.Length - 1;
            while (index >= 0 && node is not null && node.Value.Character == pattern[index])
            {
                index--;
                node = node.Previous;
            }

            if (index >= 0)
            {
                continue;
            }

            node = _pending.Last;
            for (int matched = 0; matched < pattern.Length; matched++)
            {
                node!.Value.Redacted = true;
                node = node.Previous;
            }
        }
    }

    private void EmitFirst(Action<string, string> emit)
    {
        PendingCharacter character = _pending.First!.Value;
        _pending.RemoveFirst();
        if (character.Redacted)
        {
            if (!_insideRedaction)
            {
                emit(character.Scope, _marker);
                _insideRedaction = true;
            }

            return;
        }

        _insideRedaction = false;
        emit(character.Scope, character.Character.ToString());
    }

    private static string NormalizeNewlines(string value)
        => value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    private sealed class PendingCharacter
    {
        internal PendingCharacter(string scope, char character)
        {
            Scope = scope;
            Character = character;
        }

        internal char Character { get; }

        internal string Scope { get; }

        internal bool Redacted { get; set; }
    }
}

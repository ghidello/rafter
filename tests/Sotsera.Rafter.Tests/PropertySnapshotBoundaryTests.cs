using System.Collections;

namespace Sotsera.Rafter.Tests;

public sealed class PropertySnapshotBoundaryTests
{
    [Theory]
    [InlineData(1023, true)]
    [InlineData(1024, true)]
    [InlineData(1025, false)]
    public void CollectionLimitsCountItemsAndDisposeTheSingleEnumerator(int count, bool accepted)
    {
        CountingSequence sequence = new(count);
        Action snapshot = () => _ = InvocationOutput.SnapshotProperty("items", sequence);

        if (accepted)
        {
            snapshot.Should().NotThrow();
        }
        else
        {
            snapshot.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("value");
        }
        sequence.Enumerators.Should().Be(1);
        sequence.MoveCalls.Should().Be(accepted ? count + 1 : 1025);
        sequence.DisposeCalls.Should().Be(1);
    }

    [Theory]
    [InlineData(-1, true)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void CharacterBudgetIncludesNameEqualsAndQuotedLiteral(int delta, bool accepted)
    {
        // x= plus two quotes consume four characters in the canonical representation.
        string text = new('a', 1_048_576 - 4 + delta);
        Action snapshot = () => _ = InvocationOutput.SnapshotProperty("x", text);
        if (accepted)
        {
            snapshot.Should().NotThrow();
        }
        else
        {
            snapshot.Should().Throw<ArgumentException>();
        }
    }

    [Fact]
    public void InfiniteCollectionsStopAtTheFirstRejectedItemAndDispose()
    {
        CountingSequence sequence = new(int.MaxValue);
        Action snapshot = () => _ = InvocationOutput.SnapshotProperty("items", sequence);

        snapshot.Should().Throw<ArgumentException>();
        sequence.MoveCalls.Should().Be(1025);
        sequence.DisposeCalls.Should().Be(1);
    }

    [Fact]
    public void RetainedPropertiesDoNotReformatCallerOwnedMutableValues()
    {
        MutableScalar scalar = new() { Value = "before" };
        object[] values = [scalar];
        OutputProperty property = InvocationOutput.SnapshotProperty("items", values);
        scalar.Value = "after";
        values[0] = "replacement";

        property.CanonicalValue.Should().Be("[\"before\"]");
        scalar.Formats.Should().Be(1);
    }

    [Fact]
    public void StructuredAndNestedShapesAreRejectedBeforePublication()
    {
        object[] rejected =
        [
            new Dictionary<string, int>(StringComparer.Ordinal) { ["key"] = 1 }, new int[1, 1], new[] { new[] { 1 } },
            new[] { new KeyValuePair<string, int>("key", 1) }, new[] { new DictionaryEntry("key", 1) },
        ];
        foreach (object value in rejected)
        {
            Action snapshot = () => _ = InvocationOutput.SnapshotProperty("items", value);
            snapshot.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("value");
        }
    }

    private sealed class MutableScalar
    {
        internal string Value { get; set; } = string.Empty;

        internal int Formats { get; private set; }

        public override string ToString()
        {
            Formats++;
            return Value;
        }
    }

    private sealed class CountingSequence(int count) : IEnumerable
    {
        internal int Enumerators { get; private set; }

        internal int MoveCalls { get; private set; }

        internal int DisposeCalls { get; private set; }

        public IEnumerator GetEnumerator()
        {
            Enumerators++;
            return new Enumerator(this, count);
        }

        private sealed class Enumerator(CountingSequence owner, int count) : IEnumerator, IDisposable
        {
            private int _index;

            public object Current => 1;

            public bool MoveNext()
            {
                owner.MoveCalls++;
                return _index++ < count;
            }

            public void Reset() => throw new NotSupportedException();

            public void Dispose() => owner.DisposeCalls++;
        }
    }
}

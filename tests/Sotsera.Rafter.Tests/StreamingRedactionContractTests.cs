using System.Text;

namespace Sotsera.Rafter.Tests;

public sealed class StreamingRedactionContractTests
{
    [Theory]
    [InlineData("secretsecret", "secret")]
    [InlineData("aaaaaa", "aa")]
    [InlineData("ababa", "aba")]
    [InlineData("prefix\r\nsecret\r\nsuffix", "secret\r\nsuffix")]
    [InlineData("a€𐍈za€𐍈z", "a€𐍈z")]
    public void EveryChunkBoundaryPreservesOrdinalIntervalUnion(string text, string pattern)
    {
        TextRedactor redactor = TextRedactor.Create([pattern]);
        TextRedactor normalized = TextRedactor.Create([Normalize(pattern)]);
        normalized.TryRedact(Normalize(text), out string expected).Should().BeTrue();
        for (int split = 0; split <= text.Length; split++)
        {
            StreamingTextRedactor streaming = new(redactor);
            StringBuilder actual = new();
            streaming.Append("first", text[..split], (_, safe) => actual.Append(safe));
            streaming.Append("second", text[split..], (_, safe) => actual.Append(safe));
            streaming.Complete((_, safe) => actual.Append(safe));
            actual.ToString().Should().Be(expected, $"split {split} must retain the non-streaming interval semantics");
        }
    }

    [Fact]
    public void RepeatedOverlappingAndAdjacentMatchesAgreeWithTheReferenceRedactor()
    {
        Random random = new(92731);
        string[] patterns = ["aba", "bab", "aaa", "bb", "secret"];
        TextRedactor redactor = TextRedactor.Create([.. patterns]);
        for (int iteration = 0; iteration < 500; iteration++)
        {
            string text = string.Concat(Enumerable.Range(0, 20).Select(_ => patterns[random.Next(patterns.Length)]));
            redactor.TryRedact(text, out string expected).Should().BeTrue();
            StreamingTextRedactor streaming = new(redactor);
            StringBuilder actual = new();
            for (int index = 0; index < text.Length; index++)
            {
                streaming.Append(index % 2 == 0 ? "first" : "second", text[index].ToString(), (_, safe) => actual.Append(safe));
            }
            streaming.Complete((_, safe) => actual.Append(safe));
            actual.ToString().Should().Be(expected);
        }
    }

    private static string Normalize(string text)
        => text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
}

using AwesomeAssertions;
using BuildSample;
using Xunit;

public sealed class GreetingTests
{
    [Fact]
    public void Built_library_provides_the_expected_greeting()
    {
        Greeting.Text.Should().Be("Hello from the build fixture.");
    }
}

namespace Sotsera.Rafter.IntegrationTests;

public sealed class FoundationIntegrationTests
{
    [Fact]
    public void TestProcessHasAStableWorkingDirectory()
    {
        Path.IsPathFullyQualified(Environment.CurrentDirectory).Should().BeTrue();
    }
}

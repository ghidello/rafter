namespace Sotsera.Rafter.IntegrationTests;

public sealed class FoundationIntegrationTests
{
    [Fact]
    public void TestProcessHasAStableWorkingDirectory()
    {
        Assert.True(Path.IsPathFullyQualified(Environment.CurrentDirectory));
    }
}

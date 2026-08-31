using System.Reflection;

namespace Sotsera.Rafter.Tests;

public sealed class FoundationTests
{
    [Fact]
    public void RuntimeAssemblyHasExpectedIdentity()
    {
        Assembly assembly = Assembly.Load("Sotsera.Rafter");

        assembly.GetName().Name.Should().Be("Sotsera.Rafter");
        assembly.GetReferencedAssemblies().Should().Contain(reference => reference.Name == "Spectre.Console");
    }
}

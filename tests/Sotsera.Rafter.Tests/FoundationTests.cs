using System.Reflection;

namespace Sotsera.Rafter.Tests;

public sealed class FoundationTests
{
    [Fact]
    public void RuntimeAssemblyHasExpectedIdentity()
    {
        Assembly assembly = Assembly.Load("Sotsera.Rafter");

        Assert.Equal("Sotsera.Rafter", assembly.GetName().Name);
        Assert.Contains(assembly.GetReferencedAssemblies(), reference => reference.Name == "Spectre.Console");
    }
}

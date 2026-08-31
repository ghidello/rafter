using System.Reflection;

namespace Sotsera.Rafter.Analyzers.Tests;

public sealed class FoundationAnalyzerTests
{
    [Fact]
    public void AnalyzerAssemblyCanBeLoaded()
    {
        Assembly assembly = Assembly.Load("Sotsera.Rafter.Analyzers");

        Assert.Equal("Sotsera.Rafter.Analyzers", assembly.GetName().Name);
    }
}

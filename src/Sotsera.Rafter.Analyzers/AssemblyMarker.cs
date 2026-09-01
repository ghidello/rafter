using Microsoft.CodeAnalysis.Diagnostics;

namespace Sotsera.Rafter.Analyzers;

internal static class AssemblyMarker
{
    internal static Type RoslynAnalyzerType => typeof(DiagnosticAnalyzer);
}

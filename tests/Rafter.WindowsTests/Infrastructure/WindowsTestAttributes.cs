using System.Runtime.CompilerServices;
using Xunit;

namespace Rafter.WindowsTests.Infrastructure;

internal static class WindowsEnvironment
{
    public static bool IsWindows => OperatingSystem.IsWindows();
}

public sealed class WindowsFactAttribute : FactAttribute
{
    public WindowsFactAttribute([CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1) : base(sourceFilePath, sourceLineNumber)
    {
        Skip = "Requires Windows process APIs.";
        SkipUnless = nameof(WindowsEnvironment.IsWindows);
        SkipType = typeof(WindowsEnvironment);
    }
}

public sealed class WindowsTheoryAttribute : TheoryAttribute
{
    public WindowsTheoryAttribute([CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1) : base(sourceFilePath, sourceLineNumber)
    {
        Skip = "Requires Windows process APIs.";
        SkipUnless = nameof(WindowsEnvironment.IsWindows);
        SkipType = typeof(WindowsEnvironment);
    }
}

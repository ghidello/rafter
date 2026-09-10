namespace Sotsera.Rafter;

/// <summary>Writes semantic output for a Rafter invocation.</summary>
public sealed class RafterOutput
{
    private readonly InvocationOutput _output;
    private readonly OutputScope _scope;

    internal RafterOutput(InvocationOutput output, OutputScope scope)
    {
        _output = output;
        _scope = scope;
    }

    internal InvocationOutput Invocation => _output;

    /// <summary>Writes an informational line.</summary>
    public void Line(string text)
    {
        using InvocationOutput.Admission admission = _output.Admit();
        ArgumentNullException.ThrowIfNull(text);
        Publish(OutputKind.Line, text);
    }

    /// <summary>Writes a successful result.</summary>
    public void Success(string text)
    {
        using InvocationOutput.Admission admission = _output.Admit();
        Publish(OutputKind.Success, RequireText(text, nameof(text)));
    }

    /// <summary>Writes a warning.</summary>
    public void Warning(string text)
    {
        using InvocationOutput.Admission admission = _output.Admit();
        Publish(OutputKind.Warning, RequireText(text, nameof(text)));
    }

    /// <summary>Writes an error with optional recovery guidance without failing the current target.</summary>
    public void Error(string text, string? recovery = null)
    {
        using InvocationOutput.Admission admission = _output.Admit();
        string message = RequireText(text, nameof(text));
        if (recovery is not null)
        {
            _ = RequireSingleLineText(recovery, nameof(recovery));
        }

        Publish(OutputKind.Error, message, recovery);
    }

    /// <summary>Writes a named scalar or one-dimensional collection property.</summary>
    public void Property(string name, object? value)
    {
        using InvocationOutput.Admission admission = _output.Admit();
        string propertyName = RequireSingleLineText(name, nameof(name));
        OutputProperty property = InvocationOutput.SnapshotProperty(propertyName, value);
        Publish(OutputKind.Property, $"{property.Name}={property.CanonicalValue}");
    }

    private static string RequireText(string text, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text, parameterName);
        return text;
    }

    private static string RequireSingleLineText(string text, string parameterName)
    {
        string result = RequireText(text, parameterName);
        if (result.Contains('\r') || result.Contains('\n'))
        {
            throw new ArgumentException("The value must be a single line.", parameterName);
        }

        return result;
    }

    private void Publish(OutputKind kind, string text, string? recovery = null)
        => _output.Publish(new OutputEvent(_scope.GetName(), kind, text, recovery));
}

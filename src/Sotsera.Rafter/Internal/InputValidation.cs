namespace Sotsera.Rafter.Internal;

internal sealed record InputValidation(string[] Diagnostics, Exception? Exception, string? Scope)
{
    public static InputValidation Evaluate(OptionSnapshot[] options, CombinedRule[] combinedRules,
        InputValues values, CancellationToken cancellationToken)
    {
        var diagnostics = new List<string>();
        var scope = "Input validation";
        try
        {
            foreach (var option in options)
            {
                var value = values.Read(option.Declaration);
                if (value is null)
                {
                    continue;
                }
                scope = $"Validation for {option.Spellings[0]}";
                foreach (var rule in option.Rules)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!rule.Predicate!(value))
                    {
                        diagnostics.Add($"{option.Spellings[0]}: {rule.Description}");
                    }
                }
            }
            if (diagnostics.Count == 0)
            {
                scope = "Combined input validation";
                var context = new InputValidationContext(values);
                foreach (var rule in combinedRules)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!rule.Predicate!(context))
                    {
                        diagnostics.Add(rule.Description);
                    }
                }
            }
        }
        catch (Exception exception) when (ExceptionPolicy.IsRecoverable(exception))
        {
            return new([.. diagnostics], exception, scope);
        }
        return new([.. diagnostics], null, null);
    }
}

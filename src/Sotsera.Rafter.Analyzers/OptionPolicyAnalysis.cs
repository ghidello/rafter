using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Sotsera.Rafter.Analyzers;

// Deliberately block-local: uncertain execution or mutation ends the current straight-line segment.
internal sealed class OptionPolicyAnalysis
{
    internal static readonly DiagnosticDescriptor Rule = new(
        "RAF004", "Configure an option presence policy once",
        "Option '{0}' already has a {1} call; remove the repeated or conflicting {2} call",
        "Rafter.Authoring", DiagnosticSeverity.Error, isEnabledByDefault: true,
        description: "Required and Default may each be selected only once and cannot be combined on one option.",
        helpLinkUri: "https://github.com/sotsera/rafter/blob/main/docs/authoring-diagnostics.md#raf004");

    private readonly OperationAnalysisContext _context;
    private readonly INamedTypeSymbol _option;
    private readonly INamedTypeSymbol _command;
    private readonly INamedTypeSymbol? _factory;
    private readonly Dictionary<ILocalSymbol, IInvocationOperation> _policies = new(SymbolEqualityComparer.Default);

    private OptionPolicyAnalysis(OperationAnalysisContext context, INamedTypeSymbol option,
        INamedTypeSymbol command, INamedTypeSymbol? factory)
    {
        _context = context;
        _option = option;
        _command = command;
        _factory = factory;
    }

    internal static void Analyze(OperationAnalysisContext context, INamedTypeSymbol option,
        INamedTypeSymbol command, INamedTypeSymbol? factory)
    {
        var analysis = new OptionPolicyAnalysis(context, option, command, factory);
        foreach (var statement in ((IBlockOperation)context.Operation).Operations)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (!analysis.AnalyzeStatement(statement))
            {
                analysis._policies.Clear();
            }
        }
    }

    private bool AnalyzeStatement(IOperation statement)
    {
        if (statement is IExpressionStatementOperation expression)
        {
            var value = expression.Operation;
            if (value is ISimpleAssignmentOperation { Target: IDiscardOperation } discard)
            {
                value = discard.Value;
            }
            return AnalyzeExpression(value);
        }

        if (statement is IVariableDeclarationGroupOperation group)
        {
            foreach (var declaration in group.Declarations)
            {
                foreach (var variable in declaration.Declarators)
                {
                    if (variable.Symbol.RefKind != RefKind.None ||
                        variable.Initializer is { } initializer && !AnalyzeExpression(initializer.Value))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        // Nested blocks are analyzed independently. Never carry state into or across branches, loops, or helpers.
        return false;
    }

    private bool AnalyzeExpression(IOperation expression)
    {
        expression = Unwrap(expression);
        if (IsSimpleValue(expression))
        {
            return true;
        }
        if (expression is not IInvocationOperation invocation)
        {
            return false;
        }

        var method = invocation.TargetMethod;
        var owner = method.ContainingType.OriginalDefinition;
        var isPolicy = SymbolEqualityComparer.Default.Equals(owner, _option) &&
            method.Name is "Required" or "Default";
        var isDeclaration =
            (SymbolEqualityComparer.Default.Equals(owner, _command) && method.Name == "Option") ||
            (SymbolEqualityComparer.Default.Equals(owner, _factory) && method.Name == "Command");
        if (!isPolicy && !isDeclaration)
        {
            return false;
        }
        if (invocation.Instance is { } instance && !IsSimpleValue(Unwrap(instance)))
        {
            return false;
        }
        foreach (var argument in invocation.Arguments)
        {
            if (!IsSimpleValue(Unwrap(argument.Value)))
            {
                return false;
            }
        }
        if (!isPolicy)
        {
            return true;
        }
        if (invocation.Instance is null || Unwrap(invocation.Instance) is not ILocalReferenceOperation local ||
            local.Local.RefKind != RefKind.None)
        {
            return false;
        }

        if (_policies.TryGetValue(local.Local, out var previous))
        {
            _context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.Syntax.GetLocation(),
                additionalLocations: [previous.Syntax.GetLocation()], properties: null,
                messageArgs: [local.Local.Name, previous.TargetMethod.Name, method.Name]));
        }
        else
        {
            _policies.Add(local.Local, invocation);
        }
        return true;
    }

    private static bool IsSimpleValue(IOperation operation)
    {
        return operation.ConstantValue.HasValue || operation is ILocalReferenceOperation { Local.RefKind: RefKind.None }
            or IParameterReferenceOperation { Parameter.RefKind: RefKind.None };
    }

    private static IOperation Unwrap(IOperation operation)
    {
        while (true)
        {
            switch (operation)
            {
                case IConversionOperation { OperatorMethod: null } conversion:
                    operation = conversion.Operand;
                    continue;
                case IParenthesizedOperation parenthesized:
                    operation = parenthesized.Operand;
                    continue;
                default:
                    return operation;
            }
        }
    }
}

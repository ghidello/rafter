using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Sotsera.Rafter.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RafterAuthoringAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor _blankDescription = new(
        "RAF001", "Provide a nonblank description",
        "Description must contain non-whitespace text",
        "Rafter.Authoring", DiagnosticSeverity.Error, isEnabledByDefault: true,
        description: "Command, option, target, and validation rule descriptions explain scripts to people and agents.",
        helpLinkUri: "https://github.com/sotsera/rafter/blob/main/docs/authoring-diagnostics.md#raf001");

    private static readonly DiagnosticDescriptor _nullableOption = new(
        "RAF002", "Declare the option type without nullability",
        "Declare option type '{0}' without nullability; Required or Default determines value presence",
        "Rafter.Authoring", DiagnosticSeverity.Warning, isEnabledByDefault: true,
        description: "An optional handle already produces a nullable lookup. Required and Default provide present values.",
        helpLinkUri: "https://github.com/sotsera/rafter/blob/main/docs/authoring-diagnostics.md#raf002");

    private static readonly DiagnosticDescriptor _discardedProcess = new(
        "RAF003", "Observe the process operation",
        "Await, return, or retain this process task and observe it before the body completes",
        "Rafter.Authoring", DiagnosticSeverity.Error, isEnabledByDefault: true,
        description: "A discarded process task can outlive its owning command or target body.",
        helpLinkUri: "https://github.com/sotsera/rafter/blob/main/docs/authoring-diagnostics.md#raf003");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [_blankDescription, _nullableOption, _discardedProcess, OptionPolicyAnalysis.Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var command = FindRafterType(start.Compilation, "Command");
            var process = FindRafterType(start.Compilation, "ProcessCommand");
            var factory = FindRafterType(start.Compilation, "Rafter");
            var option = FindRafterType(start.Compilation, "Option`1");
            var valueOption = FindRafterType(start.Compilation, "ValueOption`1");
            if (command is null || process is null)
            {
                return;
            }

            if (option is not null)
            {
                start.RegisterOperationAction(operationContext =>
                    OptionPolicyAnalysis.Analyze(operationContext, option, command, factory), OperationKind.Block);
            }

            start.RegisterOperationAction(operationContext =>
            {
                var invocation = (IInvocationOperation)operationContext.Operation;
                var method = invocation.TargetMethod;
                var owner = method.ContainingType.OriginalDefinition;
                var hasDescription =
                    (method.Name == "Command" && SymbolEqualityComparer.Default.Equals(owner, factory)) ||
                    (method.Name is "Option" or "Target" or "Validate" && SymbolEqualityComparer.Default.Equals(owner, command)) ||
                    (method.Name == "Validate" && (SymbolEqualityComparer.Default.Equals(owner, option) ||
                        SymbolEqualityComparer.Default.Equals(owner, valueOption)));
                if (hasDescription)
                {
                    AnalyzeDescription(operationContext, invocation);
                }
                if (method.Name == "Option" && method.TypeArguments.Length == 1 &&
                    SymbolEqualityComparer.Default.Equals(method.ContainingType, command))
                {
                    AnalyzeOption(operationContext, invocation);
                }
                if (method.Name == "RunAsync" &&
                    SymbolEqualityComparer.Default.Equals(method.ContainingType, process) &&
                    IsDiscarded(invocation))
                {
                    operationContext.ReportDiagnostic(Diagnostic.Create(_discardedProcess, invocation.Syntax.GetLocation()));
                }
            }, OperationKind.Invocation);
        });
    }

    private static INamedTypeSymbol? FindRafterType(Compilation compilation, string name)
    {
        var type = compilation.GetTypeByMetadataName("Sotsera.Rafter." + name);
        return type?.ContainingAssembly.Name == "Sotsera.Rafter" ? type : null;
    }

    private static void AnalyzeOption(OperationAnalysisContext context, IInvocationOperation invocation)
    {
        var type = invocation.TargetMethod.TypeArguments[0];
        if (type.NullableAnnotation != NullableAnnotation.Annotated &&
            type.OriginalDefinition.SpecialType != SpecialType.System_Nullable_T)
        {
            return;
        }

        var syntax = (InvocationExpressionSyntax)invocation.Syntax;
        var generic = syntax.Expression switch
        {
            MemberAccessExpressionSyntax member => member.Name as GenericNameSyntax,
            MemberBindingExpressionSyntax member => member.Name as GenericNameSyntax,
            GenericNameSyntax name => name,
            _ => null
        };
        var location = generic?.TypeArgumentList.Arguments[0].GetLocation() ?? syntax.GetLocation();
        context.ReportDiagnostic(Diagnostic.Create(_nullableOption, location, type.ToDisplayString()));
    }

    private static void AnalyzeDescription(OperationAnalysisContext context, IInvocationOperation invocation)
    {
        foreach (var argument in invocation.Arguments)
        {
            if (argument.Parameter?.Name != "description")
            {
                continue;
            }
            var constant = argument.Value.ConstantValue;
            if (constant.HasValue && (constant.Value is null || constant.Value is string text && string.IsNullOrWhiteSpace(text)))
            {
                context.ReportDiagnostic(Diagnostic.Create(_blankDescription, argument.Value.Syntax.GetLocation()));
            }
        }
    }

    private static bool IsDiscarded(IOperation operation)
    {
        // Follow only transparent wrappers, including ConfigureAwait on the returned Task.
        // Storing, returning, awaiting, or passing a task to a helper is outside this diagnostic.
        while (true)
        {
            switch (operation.Parent)
            {
                case IConversionOperation conversion:
                    operation = conversion;
                    continue;
                case IParenthesizedOperation parenthesized:
                    operation = parenthesized;
                    continue;
                case IConditionalAccessOperation conditional when conditional.WhenNotNull == operation:
                    operation = conditional;
                    continue;
                case IInvocationOperation configure when configure.Instance == operation &&
                    configure.TargetMethod.Name == "ConfigureAwait" &&
                    configure.TargetMethod.ContainingType.ContainingNamespace.ToDisplayString() == "System.Threading.Tasks" &&
                    configure.TargetMethod.ContainingType.Name == "Task":
                    operation = configure;
                    continue;
                case IExpressionStatementOperation:
                    return true;
                case ISimpleAssignmentOperation assignment when assignment.Value == operation:
                    return assignment.Target is IDiscardOperation;
                default:
                    return false;
            }
        }
    }
}

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Not.Analyzers.Base;

namespace Not.Analyzers.Rules.NoNestedInvocation;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class NestedInvocationAnalyzer : AnalyzerBase
{
    public const string RULE_ID = "NA0006";
    private const string TITLE = "Avoid nested method invocations";
    private const string MESSAGE_FORMAT = "Nested method invocations reduce code readability";
    private const string DESCRIPTION =
        "Method invocations should be separated into multiple statements for better readability.";

    public NestedInvocationAnalyzer()
        : base(RULE_ID, TITLE, MESSAGE_FORMAT, description: DESCRIPTION) { }

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeSyntaxNode, SyntaxKind.InvocationExpression);
    }

    protected override void SafeAnalyzeSyntaxNode(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            if (HasNestedInvocation(argument.Expression, context.SemanticModel))
            {
                context.ReportDiagnostic(CreateDiagnostic(invocation.GetLocation()));
                break;
            }
        }
    }

    private bool HasNestedInvocation(ExpressionSyntax expression, SemanticModel semanticModel)
    {
        // Skip if the expression is inside a lambda expression
        if (expression.Ancestors().Any(a => a is LambdaExpressionSyntax))
        {
            return false;
        }

        // Check direct nested invocation
        if (expression is InvocationExpressionSyntax nestedInvocation)
        {
            // Exclude typeof and nameof operators
            var memberAccess = nestedInvocation.Expression as MemberAccessExpressionSyntax;
            var identifier = nestedInvocation.Expression as IdentifierNameSyntax;
            var methodName = (memberAccess?.Name ?? identifier)?.Identifier.Text;

            if (methodName is "typeof" or "nameof")
                return false;

            return true;
        }

        // Check descendant invocations
        foreach (var descendant in expression.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            // Skip if the descendant is inside a lambda expression
            if (descendant.Ancestors().Any(a => a is LambdaExpressionSyntax))
                continue;

            var memberAccess = descendant.Expression as MemberAccessExpressionSyntax;
            var identifier = descendant.Expression as IdentifierNameSyntax;
            var methodName = (memberAccess?.Name ?? identifier)?.Identifier.Text;

            if (methodName is not "typeof" and not "nameof")
                return true;
        }

        return false;
    }
}

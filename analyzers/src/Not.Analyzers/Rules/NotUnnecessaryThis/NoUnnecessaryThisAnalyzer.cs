using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Not.Analyzers.Base;

namespace Not.Analyzers.Rules.NotUnnecessaryThis;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class NoUnnecessaryThisAnalyzer : AnalyzerBase
{
    public const string RULE_ID = "NA0005";

    public NoUnnecessaryThisAnalyzer()
        : base(
            diagnosticId: RULE_ID,
            title: "Unnecessary use of 'this'",
            messageFormat: "Unnecessary use of 'this' keyword",
            description: "Avoid using 'this' keyword when accessing class members."
        ) { }

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeSyntaxNode, SyntaxKind.ThisExpression);
    }

    protected override void SafeAnalyzeSyntaxNode(SyntaxNodeAnalysisContext context)
    {
        var thisExpression = (ThisExpressionSyntax)context.Node;

        if (thisExpression.Parent is MemberAccessExpressionSyntax memberAccess)
        {
            if (memberAccess.Parent is InvocationExpressionSyntax invocation)
            {
                var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation);
                if (symbolInfo.Symbol is IMethodSymbol methodSymbol && methodSymbol.IsExtensionMethod)
                {
                    return;
                }
            }

            if (memberAccess.Parent is ArgumentSyntax)
            {
                return;
            }

            context.ReportDiagnostic(CreateDiagnostic(thisExpression.GetLocation()));
        }
    }
}

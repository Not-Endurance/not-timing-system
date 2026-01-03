using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Not.Analyzers.Base;

namespace Not.Analyzers.Rules.NoLambdaMethodMembers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class NoLambdaMethodMemberAnalyzer : AnalyzerBase
{
    public const string RULE_ID = "NA0009";

    public NoLambdaMethodMemberAnalyzer()
        : base(
            RULE_ID,
            title: "Type member method using lambda arrow expression",
            messageFormat: "Method '{0}' should use braces and return statement instead of lambda arrow",
            description: "Type member methods should use braces and return statement instead of lambda arrow expressions. This rule does not apply to local functions or lambda expressions."
        ) { }

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeSyntaxNode, SyntaxKind.MethodDeclaration);
    }

    protected override void SafeAnalyzeSyntaxNode(SyntaxNodeAnalysisContext context)
    {
        var methodDeclaration = (MethodDeclarationSyntax)context.Node;

        if (
            !methodDeclaration.Parent.IsKind(SyntaxKind.ClassDeclaration)
            && !methodDeclaration.Parent.IsKind(SyntaxKind.StructDeclaration)
            && !methodDeclaration.Parent.IsKind(SyntaxKind.InterfaceDeclaration)
            && !methodDeclaration.Parent.IsKind(SyntaxKind.RecordDeclaration)
        )
        {
            return;
        }

        if (methodDeclaration.ExpressionBody != null)
        {
            var diagnostic = CreateDiagnostic(
                methodDeclaration.ExpressionBody.GetLocation(),
                methodDeclaration.Identifier.Text
            );
            context.ReportDiagnostic(diagnostic);
        }
    }
}

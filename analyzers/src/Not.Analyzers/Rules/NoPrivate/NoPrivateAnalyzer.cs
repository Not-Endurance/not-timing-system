using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Not.Analyzers.Base;

namespace Not.Analyzers.Rules.NoPrivate;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class NoPrivateAnalyzer : AnalyzerBase
{
    public const string RULE_ID = "NA0001";

    public NoPrivateAnalyzer()
        : base(
            diagnosticId: RULE_ID,
            title: "Avoid using 'private' keyword",
            messageFormat: "The 'private' keyword is not necessary as members are private by default",
            description: "Avoid using 'private' keyword."
        ) { }

    protected override void SafeAnalyzeSyntaxNode(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not MemberDeclarationSyntax declaration)
        {
            return;
        }

        var privateModifier = declaration.Modifiers.FirstOrDefault(m => m.IsKind(SyntaxKind.PrivateKeyword));
        if (privateModifier != default)
        {
            context.ReportDiagnostic(CreateDiagnostic(privateModifier.GetLocation()));
        }
    }

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            AnalyzeSyntaxNode,
            SyntaxKind.FieldDeclaration,
            SyntaxKind.MethodDeclaration,
            SyntaxKind.PropertyDeclaration,
            SyntaxKind.ConstructorDeclaration,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.RecordDeclaration,
            SyntaxKind.StructDeclaration
        );
    }
}

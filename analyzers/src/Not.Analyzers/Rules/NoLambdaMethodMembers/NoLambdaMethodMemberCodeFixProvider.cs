using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Not.Analyzers.Base;

namespace Not.Analyzers.Rules.NoLambdaMethodMembers;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NoLambdaMethodMemberCodeFixProvider)), Shared]
public class NoLambdaMethodMemberCodeFixProvider : TypeMemberCodeFixProvider
{
    public NoLambdaMethodMemberCodeFixProvider()
        : base("Convert to block body", NoLambdaMethodMemberAnalyzer.RULE_ID) { }

    protected override async Task<Document> SafeCodeFixAction(
        Document document,
        CSharpSyntaxNode node,
        CancellationToken cancellationToken
    )
    {
        var methodDecl = (MethodDeclarationSyntax)node;
        var expressionBody = methodDecl.ExpressionBody!.Expression;

        // Create either a return statement or expression statement based on return type
        StatementSyntax statement =
            methodDecl.ReturnType is PredefinedTypeSyntax predefinedType
            && predefinedType.Keyword.IsKind(SyntaxKind.VoidKeyword)
                ? SyntaxFactory.ExpressionStatement(expressionBody)
                : SyntaxFactory.ReturnStatement(expressionBody);

        var blockSyntax = SyntaxFactory.Block(statement);

        var newMethod = methodDecl
            .WithExpressionBody(null)
            .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.None))
            .WithBody(blockSyntax)
            .WithAdditionalAnnotations(Formatter.Annotation);

        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var newRoot = root!.ReplaceNode(methodDecl, newMethod);

        return document.WithSyntaxRoot(newRoot);
    }
}

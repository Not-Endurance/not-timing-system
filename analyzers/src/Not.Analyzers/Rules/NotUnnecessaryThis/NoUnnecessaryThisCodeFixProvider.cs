using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Not.Analyzers.Base;

namespace Not.Analyzers.Rules.NotUnnecessaryThis;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(NoUnnecessaryThisCodeFixProvider)), Shared]
public class NoUnnecessaryThisCodeFixProvider : CodeFixProviderBase
{
    public NoUnnecessaryThisCodeFixProvider()
        : base("Remove unnecessary 'this'", NoUnnecessaryThisAnalyzer.RULE_ID) { }

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var diagnostic = context.Diagnostics[0];
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        if (root?.FindNode(diagnosticSpan) is not ThisExpressionSyntax thisExpression)
        {
            return;
        }

        RegisterCodeFix(context, thisExpression, diagnostic);
    }

    protected override async Task<Document> SafeCodeFixAction(
        Document document,
        CSharpSyntaxNode node,
        CancellationToken cancellationToken
    )
    {
        if (node is not ThisExpressionSyntax thisExpression)
        {
            return document;
        }
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return document;
        }
        if (thisExpression.Parent is not MemberAccessExpressionSyntax memberAccess)
        {
            return document;
        }
        var newRoot = root.ReplaceNode(memberAccess, memberAccess.Name);
        return document.WithSyntaxRoot(newRoot);
    }
}

using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Not.Analyzers.Base;

public abstract class TypeMemberCodeFixProvider : CodeFixProviderBase
{
    public TypeMemberCodeFixProvider(string title, string ruleId)
        : base(title, ruleId) { }

    public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var diagnostic = context.Diagnostics[0];
        var diagnosticSpan = diagnostic.Location.SourceSpan;

        var declaration = root
            ?.FindToken(diagnosticSpan.Start)
            .Parent?.AncestorsAndSelf()
            .OfType<MemberDeclarationSyntax>()
            .First();
        if (declaration == null)
        {
            return;
        }

        RegisterCodeFix(context, declaration, diagnostic);
    }
}

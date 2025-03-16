using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;

namespace Not.Analyzers.Base;

public abstract class CodeFixProviderBase : CodeFixProvider
{
    readonly string _codeFixProviderName;
    private readonly string _title;
    private readonly string _ruleId;

    public CodeFixProviderBase(string title, string ruleId)
    {
        _codeFixProviderName = GetType().Name;
        _title = title;
        this._ruleId = ruleId;
    }

    public sealed override ImmutableArray<string> FixableDiagnosticIds => [_ruleId];

    public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    protected abstract Task<Document> SafeCodeFixAction(
        Document document,
        CSharpSyntaxNode node,
        CancellationToken cancellationToken
    );

    protected void RegisterCodeFix(CodeFixContext context, CSharpSyntaxNode node, Diagnostic diagnostic)
    {
        var action = CodeAction.Create(
            title: _title,
            createChangedDocument: c => CodeFixAction(context.Document, node, c),
            equivalenceKey: _codeFixProviderName
        );
        context.RegisterCodeFix(action, diagnostic);
    }

    public async Task<Document> CodeFixAction(
        Document document,
        CSharpSyntaxNode node,
        CancellationToken cancellationToken
    )
    {
        try
        {
            return await SafeCodeFixAction(document, node, cancellationToken);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"'{_codeFixProviderName}' error: {ex}");
            return document;
        }
    }
}

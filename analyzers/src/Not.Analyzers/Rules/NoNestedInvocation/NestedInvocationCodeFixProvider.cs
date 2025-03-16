using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Not.Analyzers.Base;

namespace Not.Analyzers.Rules.NoNestedInvocation;

[ExportCodeFixProvider(LanguageNames.CSharp)]
public class NestedInvocationCodeFixProvider : CodeFixProviderBase
{
    public NestedInvocationCodeFixProvider()
        : base("Extract nested invocations", NestedInvocationAnalyzer.RULE_ID) { }

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;
        if (root?.FindNode(diagnosticSpan) is InvocationExpressionSyntax invocation)
        {
            RegisterCodeFix(context, invocation, diagnostic);
        }
    }

    protected override async Task<Document> SafeCodeFixAction(
        Document document,
        CSharpSyntaxNode node,
        CancellationToken cancellationToken
    )
    {
        var invocation = (InvocationExpressionSyntax)node;
        var root = await document.GetSyntaxRootAsync(cancellationToken);

        // Find the parent statement
        var statement = invocation.Ancestors().OfType<StatementSyntax>().First();
        if (statement.Parent is not BlockSyntax block)
        {
            return document;
        }

        // Extract variable declarations and get the modified invocation
        var (variableDeclarations, modifiedInvocation) = await ExtractNestedInvocations(
            invocation,
            document,
            cancellationToken
        );
        var newInvocation = statement.ReplaceNode(invocation, modifiedInvocation);
        var newStatements = new List<StatementSyntax>();
        // Add statements before the og invocation
        newStatements.AddRange(block.Statements.TakeWhile(s => s != statement));
        newStatements.AddRange(variableDeclarations.Select(d => d.WithLeadingTrivia(statement.GetLeadingTrivia())));
        newStatements.Add(newInvocation);
        // Add statements after the og invocation
        newStatements.AddRange(block.Statements.SkipWhile(s => s != statement).Skip(1));

        var newBlock = block.WithStatements(SyntaxFactory.List(newStatements));
        var newRoot = root!.ReplaceNode(block, newBlock);
        return document.WithSyntaxRoot(newRoot);
    }

    private async Task<(
        List<StatementSyntax> Declarations,
        InvocationExpressionSyntax ModifiedInvocation
    )> ExtractNestedInvocations(
        InvocationExpressionSyntax invocation,
        Document document,
        CancellationToken cancellationToken
    )
    {
        var statements = new List<StatementSyntax>();
        var tempCount = 0;

        // Get method symbol to access parameter names
        var semanticModel = await document.GetSemanticModelAsync(cancellationToken);
        var methodSymbol = semanticModel?.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
        var parameters = methodSymbol?.Parameters;

        // Process each argument and create variable declarations as needed
        foreach (var (arg, index) in invocation.ArgumentList.Arguments.Select((a, i) => (a, i)))
        {
            // Skip if not a nested invocation
            if (
                arg.Expression is not InvocationExpressionSyntax
                && !arg.Expression.DescendantNodes().OfType<InvocationExpressionSyntax>().Any()
            )
            {
                continue;
            }

            // Get parameter name or fallback to a generic name
            var baseName = "temp";
            if (parameters != null && index < parameters.Value.Length)
            {
                baseName = parameters.Value[index].Name;
            }
            else if (arg.NameColon != null)
            {
                baseName = arg.NameColon.Name.Identifier.Text;
            }

            // Ensure unique name
            var tempName = baseName;
            while (statements.Any(s => ContainsVariableName(s, tempName)))
            {
                tempName = $"{baseName}{++tempCount}";
            }

            // Create variable declaration
            var declaration = SyntaxFactory.LocalDeclarationStatement(
                SyntaxFactory.VariableDeclaration(
                    SyntaxFactory.IdentifierName("var"),
                    SyntaxFactory.SeparatedList(
                        [
                            SyntaxFactory
                                .VariableDeclarator(tempName)
                                .WithInitializer(SyntaxFactory.EqualsValueClause(arg.Expression)),
                        ]
                    )
                )
            );

            statements.Add(declaration);

            // Replace the argument in the original invocation
            invocation = invocation.ReplaceNode(arg.Expression, SyntaxFactory.IdentifierName(tempName));
        }

        return (statements, invocation);
    }

    private bool ContainsVariableName(StatementSyntax statement, string name)
    {
        if (statement is LocalDeclarationStatementSyntax localDeclaration)
        {
            return localDeclaration.Declaration.Variables.Any(v => v.Identifier.Text == name);
        }
        return false;
    }
}

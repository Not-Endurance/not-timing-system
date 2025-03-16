using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Not.Analyzers.Base;
using Not.Analyzers.Members;

namespace Not.Analyzers.Rules.MemberOrder;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MemberOrderCodeFixProvider)), Shared]
public class MemberOrderCodeFixProvider : TypeMemberCodeFixProvider
{
    public MemberOrderCodeFixProvider()
        : base("Fix member ordering", MemberOrderAnalyzer.RULE_ID) { }

    protected override async Task<Document> SafeCodeFixAction(
        Document document,
        CSharpSyntaxNode node,
        CancellationToken cancellationToken
    )
    {
        if (node is not TypeDeclarationSyntax typeDeclaration)
        {
            return document;
        }

        var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);

        var orderedMembers = typeDeclaration
            .Members.OrderBy(MemberKindHelper.GetMemberKind)
            .Select(
                (member, index) =>
                    index == 0
                        ? member.WithLeadingTrivia(
                            member.GetLeadingTrivia().Where(t => !t.IsKind(SyntaxKind.EndOfLineTrivia))
                        )
                        : member
            )
            .ToList();

        var newTypeDeclaration = typeDeclaration.WithMembers(SyntaxFactory.List(orderedMembers));
        editor.ReplaceNode(typeDeclaration, newTypeDeclaration);

        return editor.GetChangedDocument();
    }
}

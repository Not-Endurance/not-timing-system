using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using Not.Analyzers.Base;
using Not.Analyzers.Members;

namespace Not.Analyzers.Rules.MemberSpacing;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(MemberSpacingCodeFixProvider)), Shared]
public class MemberSpacingCodeFixProvider : TypeMemberCodeFixProvider
{
    public MemberSpacingCodeFixProvider()
        : base("Fix member spacing", MemberSpacingAnalyzer.RULE_ID) { }

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

        var members = typeDeclaration.Members.ToList();
        for (int i = 1; i < members.Count; i++)
        {
            var currentMember = members[i - 1];
            var nextMember = members[i];

            var currentKind = MemberKindHelper.GetMemberKind(currentMember);
            var nextKind = MemberKindHelper.GetMemberKind(nextMember);

            var requiresBlankLine = MemberSpacingHelper.RequiresBlankLineBetween(currentKind, nextKind);
            var hasBlankLine = MemberSpacingHelper.HasLeadingBlankLine(nextMember);

            if (requiresBlankLine && !hasBlankLine)
            {
                var triviaList = new SyntaxTriviaList(SyntaxFactory.EndOfLine("\n")).Concat(
                    nextMember.GetLeadingTrivia()
                );
                members[i] = nextMember.WithLeadingTrivia(triviaList);
            }
            else if (!requiresBlankLine && hasBlankLine)
            {
                var trivia = nextMember
                    .GetLeadingTrivia()
                    .Where(t => !t.IsKind(SyntaxKind.EndOfLineTrivia) || !string.IsNullOrWhiteSpace(t.ToString()))
                    .ToList();
                members[i] = nextMember.WithLeadingTrivia(trivia);
            }
        }

        var newTypeDeclaration = typeDeclaration.WithMembers(SyntaxFactory.List(members));
        editor.ReplaceNode(typeDeclaration, newTypeDeclaration);

        return editor.GetChangedDocument();
    }
}

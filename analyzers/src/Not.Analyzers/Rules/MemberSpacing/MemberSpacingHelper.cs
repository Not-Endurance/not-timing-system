using System.Collections.Immutable;
using System.Collections.ObjectModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Not.Analyzers.Members;
using static Not.Analyzers.Members.MemberKind;

namespace Not.Analyzers.Rules.MemberSpacing;

public class MemberSpacingHelper
{
    private static readonly ReadOnlyCollection<MemberKind[]> MemberGroups;
    private static readonly ReadOnlyCollection<MemberKind> AlwaysSeparated;

    static MemberSpacingHelper()
    {
        var memberGroups = new List<MemberKind[]>
        {
            new[] { MemberKind.Delegate },
            new[] { PrivateConst, PrivateStaticReadonly, PublicConst, PublicStaticReadonly, PublicStaticEvent },
            new[] { PrivateReadonly, PrivateField, PrivateEvent, PublicField },
            new[] { AbstractProperty, AbstractMethod },
            new[] { PublicIndexDeclarator, PublicEvent },
        };
        var alwaysSeparated = new List<MemberKind>
        {
            PrivateCtor,
            ProtectedCtor,
            PublicCtor,
            PublicStaticMethod,
            PublicImplicitOperator,
            PublicOperator,
            PublicMethod,
            ProtectedMethod,
            InternalMethod,
            PrivateMethod,
            ProtectedClass,
            InternalClass,
            PrivateClass,
        };

        MemberGroups = new ReadOnlyCollection<MemberKind[]>(memberGroups);
        AlwaysSeparated = new ReadOnlyCollection<MemberKind>(alwaysSeparated);
    }

    public static bool RequiresBlankLineBetween(MemberKind first, MemberKind second)
    {
        if (AlwaysSeparated.Contains(first) || AlwaysSeparated.Contains(second))
        {
            return true;
        }
        foreach (var group in MemberGroups)
        {
            if (group.Contains(first) && group.Contains(second))
            {
                return false;
            }
        }

        return true;
    }

    public static bool HasLeadingBlankLine(MemberDeclarationSyntax member)
    {
        var trivia = member.GetLeadingTrivia().ToList();
        int newlineCount = 0;

        // Process trivia in reverse order until we hit a doc comment or the start
        for (int i = trivia.Count - 1; i >= 0; i--)
        {
            var t = trivia[i];

            // If we hit a doc comment, only count newlines that came before it
            if (
                t.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
                || t.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia)
            )
            {
                newlineCount = 0;
                // Count newlines before the doc comment
                for (int j = i - 1; j >= 0; j--)
                {
                    if (trivia[j].IsKind(SyntaxKind.EndOfLineTrivia))
                        newlineCount++;
                    else if (!trivia[j].IsKind(SyntaxKind.WhitespaceTrivia))
                        newlineCount = 0;
                }
                break;
            }

            if (t.IsKind(SyntaxKind.EndOfLineTrivia))
                newlineCount++;
            else if (!t.IsKind(SyntaxKind.WhitespaceTrivia))
                newlineCount = 0;
        }

        return newlineCount >= 1;
    }
}

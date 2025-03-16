using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Not.Analyzers.Base;
using Not.Analyzers.Members;

namespace Not.Analyzers.Rules.MemberSpacing;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class MemberSpacingAnalyzer : AnalyzerBase
{
    public const string RULE_ID = "NA0004";

    public MemberSpacingAnalyzer()
        : base(
            diagnosticId: RULE_ID,
            title: "Member spacing rule violation",
            messageFormat: "Invalid spacing between {0} and {1}: {2}",
            description: "Members should be separated by blank lines according to the specified convention."
        ) { }

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            AnalyzeSyntaxNode,
            SyntaxKind.ClassDeclaration,
            SyntaxKind.RecordDeclaration,
            SyntaxKind.StructDeclaration
        );
    }

    protected override void SafeAnalyzeSyntaxNode(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not TypeDeclarationSyntax typeDeclaration)
        {
            return;
        }

        var members = typeDeclaration.Members;

        for (var i = 0; i < members.Count - 1; i++)
        {
            var currentMember = members[i];
            var nextMember = members[i + 1];

            var currentKind = MemberKindHelper.GetMemberKind(currentMember);
            var nextKind = MemberKindHelper.GetMemberKind(nextMember);
            if (MemberKindHelper.AreProperties(currentKind, nextKind))
            {
                // Ignore properties as csharpier is too opinionated about them:
                // it does not agree case of annotations such as comments and attributes
                // as it always adds an empty line above it.
                continue;
            }

            var requiresBlankLine = MemberSpacingHelper.RequiresBlankLineBetween(currentKind, nextKind);
            var hasBlankLine = MemberSpacingHelper.HasLeadingBlankLine(nextMember);

            if (requiresBlankLine && !hasBlankLine || !requiresBlankLine && hasBlankLine)
            {
                var message = requiresBlankLine ? "blank line required" : "unnecessary blank line";

                var diagnostic = CreateDiagnostic(
                    typeDeclaration.GetLocation(),
                    currentKind.ToString(),
                    nextKind.ToString(),
                    message
                );
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}

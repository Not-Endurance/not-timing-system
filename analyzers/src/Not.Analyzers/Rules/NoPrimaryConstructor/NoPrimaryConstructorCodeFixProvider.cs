using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Not.Analyzers.Base;

namespace Not.Analyzers.Rules.NoPrimaryConstructor;

// [ExportCodeFixProvider(LanguageNames.CSharp)]
public class NoPrimaryConstructorCodeFixProvider : CodeFixProviderBase
{
    public NoPrimaryConstructorCodeFixProvider()
        : base("Convert to traditional constructor", NoPrimaryConstructorAnalyzer.RULE_ID) { }

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

        var diagnostic = context.Diagnostics.First();
        var diagnosticSpan = diagnostic.Location.SourceSpan;
        var declaration = root
            ?.FindToken(diagnosticSpan.Start)
            .Parent?.AncestorsAndSelf()
            .OfType<TypeDeclarationSyntax>()
            .First();

        if (declaration is null)
            return;

        RegisterCodeFix(context, declaration, diagnostic);
    }

    protected override async Task<Document> SafeCodeFixAction(
        Document document,
        CSharpSyntaxNode node,
        CancellationToken cancellationToken
    )
    {
        if (node is not TypeDeclarationSyntax typeDeclaration)
            return document;

        var parameters = typeDeclaration.ParameterList?.Parameters ?? [];

        var newDeclaration = typeDeclaration switch
        {
            RecordDeclarationSyntax recordDeclaration => ConvertRecord(recordDeclaration, parameters),
            ClassDeclarationSyntax classDeclaration => ConvertClassOrStruct(classDeclaration, parameters),
            _ => typeDeclaration,
        };

        var root = await document.GetSyntaxRootAsync(cancellationToken);
        var newRoot = root?.ReplaceNode(typeDeclaration, newDeclaration);
        return document.WithSyntaxRoot(newRoot!);
    }

    private static TypeDeclarationSyntax ConvertRecord(
        RecordDeclarationSyntax record,
        SeparatedSyntaxList<ParameterSyntax> parameters
    )
    {
        // Create properties for records (they don't inherit from primary ctor)
        var properties =
            parameters.Select(p =>
                SyntaxFactory
                    .PropertyDeclaration(p.Type!, ToPascalCase(p.Identifier.Text))
                    .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                    .WithAccessorList(
                        SyntaxFactory.AccessorList(
                            SyntaxFactory.SingletonList(
                                SyntaxFactory
                                    .AccessorDeclaration(SyntaxKind.GetAccessorDeclaration)
                                    .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                            )
                        )
                    )
                    .WithLeadingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed)
                    .WithTrailingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed)
            ) ?? [];

        var constructor = CreateConstructor(record.Identifier, parameters);

        var members = properties.Cast<MemberDeclarationSyntax>().Concat([constructor]);

        return record
            .WithParameterList(null)
            .WithOpenBraceToken(CreateOpenBraceToken())
            .WithCloseBraceToken(CreateCloseBraceToken())
            .WithMembers(SyntaxFactory.List(members));
    }

    private static TypeDeclarationSyntax ConvertClassOrStruct(
        ClassDeclarationSyntax classDeclaration,
        SeparatedSyntaxList<ParameterSyntax> parameters
    )
    {
        var constructor = CreateConstructor(classDeclaration.Identifier, parameters);

        return classDeclaration
            .WithParameterList(null)
            .WithOpenBraceToken(CreateOpenBraceToken())
            .WithMembers(
                SyntaxFactory.List(
                    classDeclaration.Members.Select(m =>
                    {
                        if (m is PropertyDeclarationSyntax prop)
                        {
                            return prop.WithInitializer(null)
                                .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.None))
                                .WithLeadingTrivia(SyntaxFactory.ElasticCarriageReturnLineFeed);
                        }
                        return m;
                    })
                )
            )
            .AddMembers(constructor);
    }

    private static ConstructorDeclarationSyntax CreateConstructor(
        SyntaxToken identifier,
        SeparatedSyntaxList<ParameterSyntax> parameters
    )
    {
        var assignments = parameters.Select(p =>
            SyntaxFactory.ExpressionStatement(
                SyntaxFactory.AssignmentExpression(
                    SyntaxKind.SimpleAssignmentExpression,
                    SyntaxFactory.IdentifierName(ToPascalCase(p.Identifier.Text)),
                    SyntaxFactory.IdentifierName(ToCamelCase(p.Identifier.Text))
                )
            )
        );

        return SyntaxFactory
            .ConstructorDeclaration(identifier)
            .WithModifiers(SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
            .WithParameterList(
                SyntaxFactory.ParameterList(
                    SyntaxFactory.SeparatedList(
                        parameters.Select(p =>
                            p.WithIdentifier(SyntaxFactory.Identifier(ToCamelCase(p.Identifier.Text)))
                        )
                    )
                )
            )
            .WithBody(SyntaxFactory.Block(assignments))
            .WithLeadingTrivia(
                SyntaxFactory.TriviaList(SyntaxFactory.ElasticCarriageReturnLineFeed, SyntaxFactory.Whitespace("    "))
            );
    }

    private static SyntaxToken CreateOpenBraceToken() =>
        SyntaxFactory.Token(
            SyntaxFactory.TriviaList(SyntaxFactory.ElasticCarriageReturnLineFeed),
            SyntaxKind.OpenBraceToken,
            SyntaxFactory.TriviaList()
        );

    private static SyntaxToken CreateCloseBraceToken() =>
        SyntaxFactory.Token(
            SyntaxFactory.TriviaList(SyntaxFactory.ElasticCarriageReturnLineFeed),
            SyntaxKind.CloseBraceToken,
            SyntaxFactory.TriviaList()
        );

    private static string ToPascalCase(string value) => char.ToUpper(value[0]) + value.Substring(1);

    private static string ToCamelCase(string value) => char.ToLower(value[0]) + value.Substring(1);
}

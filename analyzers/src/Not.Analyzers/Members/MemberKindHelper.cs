using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Not.Analyzers.Members;

public class MemberKindHelper
{
    public static MemberKind GetMemberKind(MemberDeclarationSyntax member)
    {
        return member switch
        {
            FieldDeclarationSyntax field => GetFieldKind(field),
            ConstructorDeclarationSyntax ctor => GetConstructorKind(ctor),
            PropertyDeclarationSyntax prop => GetPropertyKind(prop),
            MethodDeclarationSyntax method => GetMethodKind(method),
            ClassDeclarationSyntax nestedClass => GetNestedClassKind(nestedClass),
            EventFieldDeclarationSyntax eventField => GetEventFieldKind(eventField),
            OperatorDeclarationSyntax _ => MemberKind.PublicOperator,
            IndexerDeclarationSyntax _ => MemberKind.PublicIndexDeclarator,
            ConversionOperatorDeclarationSyntax _ => MemberKind.PublicImplicitOperator,
            DelegateDeclarationSyntax _ => MemberKind.Delegate,
            _ => throw new ArgumentException($"Unsupported member type: {member.GetType().Name}"),
        };
    }

    public static bool AreProperties(MemberKind first, MemberKind second)
    {
        return first == MemberKind.PrivateProperty
            || second == MemberKind.PrivateProperty
            || first == MemberKind.ProtectedProperty
            || second == MemberKind.ProtectedProperty
            || first == MemberKind.InternalProperty
            || second == MemberKind.InternalProperty
            || first == MemberKind.PublicProperty
            || second == MemberKind.PublicProperty;
    }

    private static MemberKind GetFieldKind(FieldDeclarationSyntax field)
    {
        bool isConst = field.Modifiers.Any(SyntaxKind.ConstKeyword);
        bool isStatic = field.Modifiers.Any(SyntaxKind.StaticKeyword);
        bool isReadonly = field.Modifiers.Any(SyntaxKind.ReadOnlyKeyword);
        bool isPrivate =
            field.Modifiers.Any(SyntaxKind.PrivateKeyword)
            || !field.Modifiers.Any(m => m.IsKind(SyntaxKind.PublicKeyword));
        bool isPublic = field.Modifiers.Any(SyntaxKind.PublicKeyword);

        return (isConst, isStatic, isReadonly, isPrivate, isPublic) switch
        {
            (true, _, _, true, _) => MemberKind.PrivateConst,
            (true, _, _, _, true) => MemberKind.PublicConst,
            (_, true, true, true, _) => MemberKind.PrivateStaticReadonly,
            (_, true, true, _, true) => MemberKind.PublicStaticReadonly,
            (_, _, true, true, _) => MemberKind.PrivateReadonly,
            (_, _, false, true, _) => MemberKind.PrivateField,
            (_, _, false, _, true) => MemberKind.PublicField,
            _ => throw new ArgumentException(
                $"Unexpected field configuration: name: {field.TryGetInferredMemberName()}, isConst: {isConst}"
                    + $", isStatic: {isStatic}, isReadonly: {isReadonly}, isPrivate: {isPrivate}, isPublic: {isPublic}"
            ),
        };
    }

    private static MemberKind GetEventFieldKind(EventFieldDeclarationSyntax eventField)
    {
        var isPublic = eventField.Modifiers.Any(SyntaxKind.StaticKeyword);
        var isStatic = eventField.Modifiers.Any(SyntaxKind.StaticKeyword);
        if (isPublic && isStatic)
        {
            return MemberKind.PublicStaticEvent;
        }
        if (eventField.Modifiers.Any(SyntaxKind.PublicKeyword))
        {
            return MemberKind.PublicEvent;
        }
        return MemberKind.PrivateEvent;
    }

    private static MemberKind GetConstructorKind(ConstructorDeclarationSyntax ctor)
    {
        if (ctor.Modifiers.Any(SyntaxKind.PublicKeyword))
            return MemberKind.PublicCtor;
        if (ctor.Modifiers.Any(SyntaxKind.ProtectedKeyword))
            return MemberKind.ProtectedCtor;
        return MemberKind.PrivateCtor;
    }

    private static MemberKind GetPropertyKind(PropertyDeclarationSyntax prop)
    {
        bool isAbstract = prop.Modifiers.Any(SyntaxKind.AbstractKeyword);
        if (isAbstract)
            return MemberKind.AbstractProperty;

        if (prop.Modifiers.Any(SyntaxKind.PublicKeyword))
            return MemberKind.PublicProperty;
        if (prop.Modifiers.Any(SyntaxKind.ProtectedKeyword))
            return MemberKind.ProtectedProperty;
        if (prop.Modifiers.Any(SyntaxKind.InternalKeyword))
            return MemberKind.InternalProperty;
        return MemberKind.PrivateProperty;
    }

    private static MemberKind GetMethodKind(MethodDeclarationSyntax method)
    {
        bool isAbstract = method.Modifiers.Any(SyntaxKind.AbstractKeyword);
        if (isAbstract)
            return MemberKind.AbstractMethod;

        if (method.Modifiers.Any(SyntaxKind.PublicKeyword) && method.Modifiers.Any(SyntaxKind.StaticKeyword))
            return MemberKind.PublicStaticMethod;
        if (method.Modifiers.Any(SyntaxKind.PublicKeyword))
            return MemberKind.PublicMethod;
        if (method.Modifiers.Any(SyntaxKind.ProtectedKeyword))
            return MemberKind.ProtectedMethod;
        if (method.Modifiers.Any(SyntaxKind.InternalKeyword))
            return MemberKind.InternalMethod;
        return MemberKind.PrivateMethod;
    }

    private static MemberKind GetNestedClassKind(ClassDeclarationSyntax nestedClass)
    {
        if (nestedClass.Modifiers.Any(SyntaxKind.InternalKeyword))
            return MemberKind.InternalClass;
        return MemberKind.PrivateClass;
    }
}

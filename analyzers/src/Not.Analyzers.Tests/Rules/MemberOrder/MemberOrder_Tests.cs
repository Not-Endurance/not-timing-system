using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Not.Analyzers.Members;
using Not.Analyzers.Rules.MemberOrder;

namespace Not.Analyzers.Tests.Rules.MemberOrder;

public class MemberOrderTests
{
    [Fact]
    public async Task PrivateConstantsBeforePublicConstants()
    {
        var test = """
            public class MyClass 
            {
                public const int PUBLIC_CONSTANT = 1;
                private const int PRIVATE_CONSTANT = 2;
            }
            """;

        var expected = """
            public class MyClass 
            {
                private const int PRIVATE_CONSTANT = 2;
                public const int PUBLIC_CONSTANT = 1;
            }
            """;

        await VerifyFix(test, expected, MemberKind.PublicConst, MemberKind.PrivateConst);
    }

    [Fact]
    public async Task PrivateStaticReadonlyBeforePublicStaticReadonly()
    {
        var test = """
            public class MyClass 
            {
                public static readonly int PublicField = 1;
                private static readonly int PrivateField = 2;
            }
            """;

        var expected = """
            public class MyClass 
            {
                private static readonly int PrivateField = 2;
                public static readonly int PublicField = 1;
            }
            """;

        await VerifyFix(test, expected, MemberKind.PublicStaticReadonly, MemberKind.PrivateStaticReadonly);
    }

    [Fact]
    public async Task PrivateReadonlyBeforePrivateField()
    {
        var test = """
            public class MyClass 
            {
                private string _regularField;
                private readonly string _readonlyField;
            }
            """;

        var expected = """
            public class MyClass 
            {
                private readonly string _readonlyField;
                private string _regularField;
            }
            """;

        await VerifyFix(test, expected, MemberKind.PrivateField, MemberKind.PrivateReadonly);
    }

    [Fact]
    public async Task PrivateConstructorBeforeProtectedConstructor()
    {
        var test = """
            public class MyClass 
            {
                protected MyClass(int x) { }
                private MyClass() { }
            }
            """;

        var expected = """
            public class MyClass 
            {
                private MyClass() { }
                protected MyClass(int x) { }
            }
            """;

        await VerifyFix(test, expected, MemberKind.ProtectedCtor, MemberKind.PrivateCtor);
    }

    [Fact]
    public async Task AbstractPropertyBeforeProtectedProperty()
    {
        var test = """
            public abstract class MyClass 
            {
                protected string ProtectedProperty { get; set; }
                public abstract string AbstractProperty { get; }
            }
            """;

        var expected = """
            public abstract class MyClass 
            {
                public abstract string AbstractProperty { get; }
                protected string ProtectedProperty { get; set; }
            }
            """;

        await VerifyFix(test, expected, MemberKind.ProtectedProperty, MemberKind.AbstractProperty);
    }

    [Fact]
    public async Task AbstractMethodBeforeProtectedMethod()
    {
        var test = """
            public abstract class MyClass 
            {
                protected void ProtectedMethod() { }
                protected abstract void AbstractMethod();
            }
            """;

        var expected = """
            public abstract class MyClass 
            {
                protected abstract void AbstractMethod();
                protected void ProtectedMethod() { }
            }
            """;

        await VerifyFix(test, expected, MemberKind.ProtectedMethod, MemberKind.AbstractMethod);
    }

    [Fact]
    public async Task InternalClassBeforePrivateClass()
    {
        var test = """
            public class MyClass 
            {
                private class PrivateNested { }
                internal class InternalNested { }
            }
            """;

        var expected = """
            public class MyClass 
            {
                internal class InternalNested { }
                private class PrivateNested { }
            }
            """;

        await VerifyFix(test, expected, MemberKind.PrivateClass, MemberKind.InternalClass);
    }

    [Fact]
    public async Task ProtectedConstructorBeforePublicConstructor()
    {
        var test = """
            public class MyClass 
            {
                public MyClass(int x) { }
                protected MyClass() { }
            }
            """;

        var expected = """
            public class MyClass 
            {
                protected MyClass() { }
                public MyClass(int x) { }
            }
            """;

        await VerifyFix(test, expected, MemberKind.PublicCtor, MemberKind.ProtectedCtor);
    }

    [Fact]
    public async Task PublicConstructorBeforePrivateProperty()
    {
        var test = """
            public class MyClass 
            {
                private string PrivateProperty { get; set; }
                public MyClass() { }
            }
            """;

        var expected = """
            public class MyClass 
            {
                public MyClass() { }
                private string PrivateProperty { get; set; }
            }
            """;

        await VerifyFix(test, expected, MemberKind.PrivateProperty, MemberKind.PublicCtor);
    }

    [Fact]
    public async Task AbstractMethodBeforePublicProperty()
    {
        var test = """
            public abstract class MyClass 
            {
                public string PublicProperty { get; set; }
                protected abstract void AbstractMethod();
            }
            """;

        var expected = """
            public abstract class MyClass 
            {
                protected abstract void AbstractMethod();
                public string PublicProperty { get; set; }
            }
            """;

        await VerifyFix(test, expected, MemberKind.PublicProperty, MemberKind.AbstractMethod);
    }

    [Fact]
    public async Task InternalPropertyBeforePublicProperty()
    {
        var test = """
            public class MyClass 
            {
                public string PublicProperty { get; set; }
                internal string InternalProperty { get; set; }
            }
            """;

        var expected = """
            public class MyClass 
            {
                internal string InternalProperty { get; set; }
                public string PublicProperty { get; set; }
            }
            """;

        await VerifyFix(test, expected, MemberKind.PublicProperty, MemberKind.InternalProperty);
    }

    [Fact]
    public async Task InternalMethodBeforePublicMethod()
    {
        var test = """
            public class MyClass 
            {
                public void PublicMethod() { }
                internal void InternalMethod() { }
            }
            """;

        var expected = """
            public class MyClass 
            {
                internal void InternalMethod() { }
                public void PublicMethod() { }
            }
            """;

        await VerifyFix(test, expected, MemberKind.PublicMethod, MemberKind.InternalMethod);
    }

    [Fact]
    public async Task InternalMethodBeforePrivateClass()
    {
        var test = """
            public class MyClass 
            {
                private class PrivateNested { }
                internal void InternalMethod() { }
            }
            """;

        var expected = """
            public class MyClass 
            {
                internal void InternalMethod() { }
                private class PrivateNested { }
            }
            """;

        await VerifyFix(test, expected, MemberKind.PrivateClass, MemberKind.InternalMethod);
    }

    private static async Task VerifyFix(string test, string expected, MemberKind currentMember, MemberKind nextMember)
    {
        var analyzer = new MemberOrderAnalyzer();
        var tree = CSharpSyntaxTree.ParseText(test);
        var root = tree.GetRoot();
        var firstMember = root.DescendantNodes().OfType<MemberDeclarationSyntax>().First();

        var context = new CSharpCodeFixTest<MemberOrderAnalyzer, MemberOrderCodeFixProvider, DefaultVerifier>
        {
            ReferenceAssemblies = ReferenceAssemblies.Net.Net80,
            TestCode = test,
            FixedCode = expected,
        };

        await context.RunAsync();
    }
}

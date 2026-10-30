using Not.Guids;

namespace NTS.Tests.Unit.Application;

/// <summary>
/// A name-based Guid is the same for the same namespace and name, and is the one the standard gives: RFC 4122 names the
/// namespace DNS and the name "python.org" as an example.
/// </summary>
public sealed class NameBasedGuidTests
{
    static readonly Guid NAMESPACE_DNS = new("6ba7b810-9dad-11d1-80b4-00c04fd430c8");

    [Fact]
    public void It_is_the_Guid_the_standard_gives_for_a_namespace_and_a_name()
    {
        Assert.Equal(
            new Guid("886313e1-3b8a-5372-9b90-0c9aee199e5d"),
            NameBasedGuid.Create(NAMESPACE_DNS, "python.org")
        );
    }

    [Fact]
    public void The_same_namespace_and_name_make_the_same_Guid_and_another_name_makes_another()
    {
        var first = NameBasedGuid.Create(NAMESPACE_DNS, "ana/event");

        Assert.Equal(first, NameBasedGuid.Create(NAMESPACE_DNS, "ana/event"));
        Assert.NotEqual(first, NameBasedGuid.Create(NAMESPACE_DNS, "boris/event"));
    }

    [Fact]
    public void It_is_a_version_5_Guid_of_the_standard_variant()
    {
        var text = NameBasedGuid.Create(NAMESPACE_DNS, "python.org").ToString();

        Assert.Equal('5', text[14]);
        Assert.Contains(text[19], "89ab");
    }
}

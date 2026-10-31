using System.Text.Json;
using NoTiming.Ui.Features.Console;

namespace NTS.Tests.Unit.NoTiming.Ui.Features.Console;

/// <summary>What the Console reads of the attributes the Api answered: a flag is true only when the Api says true, and a text is a string.</summary>
public sealed class JsonAttributesTests
{
    [Theory]
    [InlineData("{\"may\":true}", true)]
    [InlineData("{\"may\":false}", false)]
    [InlineData("{\"may\":null}", false)]
    [InlineData("{\"may\":\"true\"}", false)]
    [InlineData("{\"may\":1}", false)]
    [InlineData("{\"other\":true}", false)]
    public void A_flag_is_true_only_when_the_Api_says_true(string attributes, bool expected)
    {
        Assert.Equal(expected, JsonAttributes.Flag(Parse(attributes), "may"));
    }

    [Theory]
    [InlineData("{\"name\":\"Land of Test\"}", "Land of Test")]
    [InlineData("{\"name\":null}", null)]
    [InlineData("{\"name\":3}", null)]
    [InlineData("{\"other\":\"x\"}", null)]
    public void A_text_is_the_string_the_Api_says_and_none_otherwise(string attributes, string? expected)
    {
        Assert.Equal(expected, JsonAttributes.Text(Parse(attributes), "name"));
    }

    static JsonElement Parse(string json)
    {
        return JsonDocument.Parse(json).RootElement.Clone();
    }
}

using System.Net;
using System.Text.Json;
using Not.Application.HTTP;
using Not.Domain.Exceptions;
using NoTiming.Ui.Features.Console;

namespace NTS.Tests.Unit.NoTiming.Ui.Features.Console;

/// <summary>What the services of the Console make of what the Api answers (#646): a flag, a text, and the answer that took the request.</summary>
public sealed class ApiAnswersTests
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
        Assert.Equal(expected, ApiAnswers.Flag(Parse(attributes), "may"));
    }

    [Theory]
    [InlineData("{\"name\":\"Land of Test\"}", "Land of Test")]
    [InlineData("{\"name\":\"\"}", "")]
    [InlineData("{\"name\":null}", null)]
    [InlineData("{\"name\":12}", null)]
    [InlineData("{\"name\":true}", null)]
    [InlineData("{\"other\":\"Land\"}", null)]
    public void A_text_is_what_the_Api_says_when_it_says_a_string_and_none_when_it_says_anything_else(
        string attributes,
        string? expected
    )
    {
        Assert.Equal(expected, ApiAnswers.Text(Parse(attributes), "name"));
    }

    [Fact]
    public void An_answer_that_took_the_request_is_returned_as_it_is()
    {
        var answer = new JsonApiResponse(HttpStatusCode.Created, null, null, null);

        Assert.Same(answer, ApiAnswers.Accepted(answer));
    }

    [Fact]
    public void An_answer_that_did_not_take_the_request_is_the_words_of_the_Api_as_a_domain_error()
    {
        var refused = new JsonApiResponse(
            HttpStatusCode.Conflict,
            null,
            null,
            new JsonApiError(409, "event-started", "The Event has started.", "Its Setup is read only.")
        );

        var error = Assert.Throws<DomainException>(() => ApiAnswers.Accepted(refused));

        Assert.Equal("The Event has started. Its Setup is read only.", error.Message);
    }

    [Fact]
    public void An_answer_that_did_not_take_the_request_and_said_no_words_is_its_status()
    {
        var silent = new JsonApiResponse(HttpStatusCode.BadGateway, null, null, null);

        var error = Assert.Throws<DomainException>(() => ApiAnswers.Accepted(silent));

        Assert.Equal("BadGateway", error.Message);
    }

    static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}

using Microsoft.AspNetCore.Http;
using NoTiming.Api.Features.Account;

namespace NTS.Tests.Integration;

/// <summary>
/// The sign-in pages and the code email speak English, Bulgarian and Turkish (#599): every language has every text, and
/// the placeholders the code fills in are in every translation of the texts that carry them.
/// </summary>
public sealed class AccountTextTests
{
    static readonly AccountText TEXT = AccountText.Load();

    [Fact]
    public void The_platform_has_the_three_languages()
    {
        Assert.Equal(["en", "bg", "tr"], AccountText.Languages);
    }

    [Theory]
    [InlineData("bg")]
    [InlineData("tr")]
    public void A_translation_has_exactly_the_texts_of_English(string language)
    {
        var english = TEXT.Keys("en").Order().ToArray();

        Assert.Equal(english, TEXT.Keys(language).Order().ToArray());
    }

    [Theory]
    [InlineData("en")]
    [InlineData("bg")]
    [InlineData("tr")]
    public void No_text_is_empty_and_a_translation_is_not_a_copy_of_English(string language)
    {
        foreach (var key in TEXT.Keys(language))
        {
            var text = TEXT.Get(language, key);
            Assert.False(string.IsNullOrWhiteSpace(text), $"{language}: '{key}' is empty");
            if (language != "en")
            {
                Assert.NotEqual(TEXT.Get("en", key), text);
            }
        }
    }

    [Theory]
    [InlineData("en")]
    [InlineData("bg")]
    [InlineData("tr")]
    public void The_texts_that_are_filled_in_keep_their_placeholders_in_every_language(string language)
    {
        Assert.Contains("{code}", TEXT.Get(language, "email.code.body"));
        Assert.Contains("{minutes}", TEXT.Get(language, "email.code.body"));
        Assert.Contains("{seconds}", TEXT.Get(language, "code.resendIn"));
    }

    [Fact]
    public void Placeholders_are_filled_and_the_unknown_are_left_alone()
    {
        var filled = AccountText.Fill(
            "Code {code} for {minutes} minutes, {unknown}",
            new Dictionary<string, string> { ["code"] = "123456", ["minutes"] = "10" }
        );

        Assert.Equal("Code 123456 for 10 minutes, {unknown}", filled);
    }

    [Fact]
    public void A_missing_translation_falls_back_to_English_and_a_missing_text_is_an_error()
    {
        Assert.Equal(TEXT.Get("en", "heading"), TEXT.Get("de", "heading"));
        Assert.Throws<KeyNotFoundException>(() => TEXT.Get("en", "there.is.no.such.text"));
    }

    [Theory]
    [InlineData("bg", null, "bg")]
    [InlineData("tr-TR,tr;q=0.9,en;q=0.8", null, "tr")]
    [InlineData("en-US,en;q=0.9,bg;q=0.8", null, "en")]
    [InlineData("bg;q=0.5,tr;q=0.9", null, "tr")]
    [InlineData("de-DE,de;q=0.9", null, "en")]
    [InlineData("*", null, "en")]
    [InlineData(null, null, "en")]
    [InlineData("bg", "tr", "tr")]
    [InlineData("bg", "xx", "bg")]
    public void The_language_is_the_one_asked_for_then_the_best_the_browser_prefers(
        string? acceptLanguage,
        string? queryLanguage,
        string expected
    )
    {
        var context = new DefaultHttpContext();
        if (acceptLanguage != null)
        {
            context.Request.Headers.AcceptLanguage = acceptLanguage;
        }

        if (queryLanguage != null)
        {
            context.Request.QueryString = new QueryString("?lang=" + queryLanguage);
        }

        Assert.Equal(expected, TEXT.Resolve(context.Request));
    }
}

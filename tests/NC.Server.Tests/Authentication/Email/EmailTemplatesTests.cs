using System.Globalization;
using Microsoft.Extensions.Localization;
using NC.Localization;
using NC.Server.Authentication.Email;
using Xunit;

namespace NC.Server.Tests.Authentication.Email;

[Collection("Localization")]
public sealed class EmailTemplatesTests : IDisposable
{
    public EmailTemplatesTests()
    {
        LocalizationHelper.Configure(new KeyAndCultureLocalizer());
    }

    public void Dispose()
    {
        LocalizationHelper.Clear();
    }

    [Fact]
    public void SignInCode_WhenBulgarian_ShouldWriteTheSubjectInBulgarian()
    {
        var message = EmailTemplates.SignInCode("person@example.com", "bg", "123456");

        Assert.Equal("Your_sign_in_code_string|bg", message.Subject);
    }

    [Fact]
    public void SignInCode_WhenLocalizerIsNotConfigured_ShouldThrow()
    {
        LocalizationHelper.Clear();

        Assert.Throws<InvalidOperationException>(() => EmailTemplates.SignInCode("person@example.com", "bg", "123456"));
    }

    [Fact]
    public void SignInCode_WhenCalled_ShouldLeaveTheCurrentCultureUnchanged()
    {
        var before = CultureInfo.CurrentUICulture.Name;

        EmailTemplates.SignInCode("person@example.com", "tr", "123456");

        Assert.Equal(before, CultureInfo.CurrentUICulture.Name);
    }

    [Fact]
    public void PasskeyAdded_WhenTurkish_ShouldWriteTheSubjectInTurkish()
    {
        var message = EmailTemplates.PasskeyAdded(
            "person@example.com",
            "tr",
            "Laptop",
            new DateTimeOffset(2026, 11, 8, 9, 30, 0, TimeSpan.Zero)
        );

        Assert.Equal("A_passkey_was_added_to_your_account_string|tr", message.Subject);
    }

    /// <summary>Answers each key with its name and the culture it was read in, so a test can see which language was used.</summary>
    sealed class KeyAndCultureLocalizer : IStringLocalizer
    {
        public LocalizedString this[string name] => new(name, $"{name}|{CultureInfo.CurrentUICulture.Name}");
        public LocalizedString this[string name, params object[] arguments] => this[name];

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        {
            return [];
        }
    }
}

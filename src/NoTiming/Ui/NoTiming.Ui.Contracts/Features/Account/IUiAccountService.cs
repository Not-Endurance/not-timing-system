using NoTiming.Application.Contracts.Account;

namespace NoTiming.Ui.Contracts.Features.Account;

/// <summary>Sign-in, registration and sign-out over the account routes. A refused request raises an HttpRequestException carrying its status.</summary>
public interface IUiAccountService
{
    Task RequestSignInCodeAsync(string email);

    Task RequestRegistrationCodeAsync(RegistrationRequest request);

    Task VerifyCodeAsync(string email, string code);

    Task SignOutAsync();
}

public sealed class RegistrationRequest
{
    public string Email { get; set; } = string.Empty;

    public string GivenName { get; set; } = string.Empty;

    public string Surname { get; set; } = string.Empty;

    /// <summary>The NoTiming profile: the country is the only field the registration requires.</summary>
    public NoTimingProfile Profile { get; set; } = new();

    /// <summary>A field no person fills in. A bot does, and gets the normal answer and nothing else.</summary>
    public string Website { get; set; } = string.Empty;
}

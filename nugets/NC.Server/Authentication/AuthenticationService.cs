using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NC.Server.Authentication.Account;
using NC.Server.Authentication.Email;
using NC.Server.Authentication.Identity;
using NC.Server.Authentication.Passkeys;
using NC.Server.Authentication.Profiles;
using NC.Server.Authentication.Sessions;
using NC.Server.Authentication.Settings;
using NC.Server.Authentication.SignIn;

namespace NC.Server.Authentication;

/// <summary>
/// The module's view of sign-in. It asks the passkey check or the code check for the user, then hands that user to
/// <see cref="SignInCompletion"/>, which starts the session. The internal services behind it are not visible outside.
/// </summary>
internal sealed class AuthenticationService<TProfile> : IAuthentication<TProfile>
    where TProfile : class, new()
{
    readonly PasskeyService _passkeys;
    readonly AccountSignInService _signIn;
    readonly AccountService _account;
    readonly SignInCompletion _completion;
    readonly EmailLanguage _languages;
    readonly IOptions<AuthenticationSettings> _settings;

    public AuthenticationService(
        PasskeyService passkeys,
        AccountSignInService signIn,
        AccountService account,
        SignInCompletion completion,
        EmailLanguage languages,
        IOptions<AuthenticationSettings> settings
    )
    {
        _passkeys = passkeys;
        _signIn = signIn;
        _account = account;
        _completion = completion;
        _languages = languages;
        _settings = settings;
    }

    public Task<string> PasskeySignInOptions(CancellationToken cancellationToken)
    {
        return _passkeys.SignInOptions();
    }

    public async Task<SignInOutcome<TProfile>> SignInWithPasskey(
        HttpContext httpContext,
        string credentialJson,
        CancellationToken cancellationToken
    )
    {
        var user = await _passkeys.VerifySignIn(credentialJson, cancellationToken);
        return user is null ? Refused() : await Complete(httpContext, user, cancellationToken);
    }

    public Task<CodeRequestOutcome> SendSignInCode(
        HttpContext httpContext,
        string email,
        CancellationToken cancellationToken
    )
    {
        return _signIn.RequestSignInCode(email, Language(httpContext), ClientAddress(httpContext), cancellationToken);
    }

    public Task<CodeRequestOutcome> RequestRegistrationCode(
        HttpContext httpContext,
        string email,
        RegistrationDetails<TProfile> details,
        CancellationToken cancellationToken
    )
    {
        return _signIn.RequestRegistrationCode(
            email,
            details.GivenName,
            details.Surname,
            ProfileDocument.ToBson(details.Profile),
            Language(httpContext),
            ClientAddress(httpContext),
            cancellationToken
        );
    }

    public async Task<SignInOutcome<TProfile>> SignInWithCode(
        HttpContext httpContext,
        string email,
        string code,
        CancellationToken cancellationToken
    )
    {
        var user = await _signIn.VerifyCode(email, code, cancellationToken);
        return user is null ? Refused() : await Complete(httpContext, user, cancellationToken);
    }

    public async Task SignOut(HttpContext httpContext, CancellationToken cancellationToken)
    {
        var name = _settings.Value.CookieName;
        await _account.SignOut(httpContext.Request.Cookies[name], cancellationToken);
        httpContext.Response.Cookies.Append(name, string.Empty, SessionCookie.Options(DateTimeOffset.UnixEpoch));
    }

    async Task<SignInOutcome<TProfile>> Complete(
        HttpContext httpContext,
        IdentityUserDocument user,
        CancellationToken cancellationToken
    )
    {
        var signedIn = await _completion.CompleteAsync(httpContext, user, cancellationToken);
        return signedIn
            ? new SignInOutcome<TProfile> { SignedIn = true, User = AuthenticatedUser<TProfile>.From(user) }
            : Refused();
    }

    string Language(HttpContext httpContext)
    {
        return _languages.Choose(httpContext.Request.Headers.AcceptLanguage.ToString());
    }

    static string ClientAddress(HttpContext httpContext)
    {
        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    static SignInOutcome<TProfile> Refused()
    {
        return new SignInOutcome<TProfile> { SignedIn = false };
    }
}

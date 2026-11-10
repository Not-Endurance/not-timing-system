using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NC.Server.Authentication.Account;
using NC.Server.Authentication.Email;
using NC.Server.Authentication.Interface;
using NC.Server.Authentication.Passkeys;
using NC.Server.Authentication.Sessions;
using NC.Server.Authentication.Settings;
using NC.Server.Authentication.SignIn;

namespace NC.Server.Authentication;

/// <summary>
/// The app's view of sign-in. It asks the passkey check or the code check for the user, then hands that user to
/// <see cref="SignInCompletion"/>, which starts the session. The internal services behind it are not visible to the app.
/// </summary>
internal sealed class AuthenticationService : IAuthentication
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

    public async Task<SignInOutcome> SignInWithPasskey(
        HttpContext httpContext,
        string credentialJson,
        CancellationToken cancellationToken
    )
    {
        var user = await _passkeys.VerifySignIn(credentialJson, cancellationToken);
        return user is null ? Refused() : await _completion.CompleteAsync(httpContext, user, cancellationToken);
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
        RegistrationDetails details,
        CancellationToken cancellationToken
    )
    {
        return _signIn.RequestRegistrationCode(
            email,
            details,
            Language(httpContext),
            ClientAddress(httpContext),
            cancellationToken
        );
    }

    public async Task<SignInOutcome> SignInWithCode(
        HttpContext httpContext,
        string email,
        string code,
        CancellationToken cancellationToken
    )
    {
        var user = await _signIn.VerifyCode(email, code, cancellationToken);
        return user is null ? Refused() : await _completion.CompleteAsync(httpContext, user, cancellationToken);
    }

    public async Task SignOut(HttpContext httpContext, CancellationToken cancellationToken)
    {
        var name = _settings.Value.CookieName;
        await _account.SignOut(httpContext.Request.Cookies[name], cancellationToken);
        httpContext.Response.Cookies.Append(name, string.Empty, SessionCookie.Options(DateTimeOffset.UnixEpoch));
    }

    public Task<int?> SignedInUserId(HttpContext httpContext, CancellationToken cancellationToken)
    {
        return SessionUser.IdOf(httpContext);
    }

    public async Task<AuthenticatedUser?> CurrentUser(HttpContext httpContext, CancellationToken cancellationToken)
    {
        var userId = await SessionUser.IdOf(httpContext);
        if (userId is null)
        {
            return null;
        }

        var user = await _account.Find(userId.Value, cancellationToken);
        return user is null ? null : AuthenticatedUser.From(user);
    }

    public async Task<ProfileUpdateOutcome> UpdateProfile(
        HttpContext httpContext,
        ProfileChanges changes,
        CancellationToken cancellationToken
    )
    {
        var userId = await SessionUser.IdOf(httpContext);
        if (userId is null)
        {
            return ProfileUpdateOutcome.NotSignedIn;
        }

        var saved = await _account.UpdateProfile(userId.Value, changes, cancellationToken);
        return saved ? ProfileUpdateOutcome.Saved : ProfileUpdateOutcome.MissingDetails;
    }

    string Language(HttpContext httpContext)
    {
        return _languages.Choose(httpContext.Request.Headers.AcceptLanguage.ToString());
    }

    static string ClientAddress(HttpContext httpContext)
    {
        return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    static SignInOutcome Refused()
    {
        return new SignInOutcome { SignedIn = false };
    }
}

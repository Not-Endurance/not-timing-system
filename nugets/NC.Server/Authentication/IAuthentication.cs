using Microsoft.AspNetCore.Http;
using NC.Server.Authentication.Account;
using NC.Server.Authentication.SignIn;

namespace NC.Server.Authentication;

/// <summary>
/// Sign-in, sign-out and the signed-in user, as the app sees them. A method that reads or writes the request or its
/// cookies takes the <see cref="HttpContext"/>: the session cookie, the language of an email and the client address are
/// read from it, and the session cookie is written to it.
/// </summary>
public interface IAuthentication
{
    /// <summary>
    /// The options a browser needs before it asks an authenticator for a passkey answer. This is the one method without
    /// <see cref="HttpContext"/>: the ceremony keeps its challenge in a cookie that it writes through the request
    /// accessor, so the call must run inside an HTTP request.
    /// </summary>
    Task<string> PasskeySignInOptions(CancellationToken cancellationToken);

    Task<SignInOutcome> SignInWithPasskey(
        HttpContext httpContext,
        string credentialJson,
        CancellationToken cancellationToken
    );

    /// <summary>Sends a sign-in code. The answer is the same whether or not the address has an account.</summary>
    Task<CodeRequestOutcome> SendSignInCode(HttpContext httpContext, string email, CancellationToken cancellationToken);

    /// <summary>Sends a registration code and keeps the details until the code is checked.</summary>
    Task<CodeRequestOutcome> RequestRegistrationCode(
        HttpContext httpContext,
        string email,
        RegistrationDetails details,
        CancellationToken cancellationToken
    );

    Task<SignInOutcome> SignInWithCode(
        HttpContext httpContext,
        string email,
        string code,
        CancellationToken cancellationToken
    );

    Task SignOut(HttpContext httpContext, CancellationToken cancellationToken);

    /// <summary>The id of the signed-in user, or null when nobody is signed in.</summary>
    Task<int?> SignedInUserId(HttpContext httpContext, CancellationToken cancellationToken);

    /// <summary>The signed-in user, or null when nobody is signed in.</summary>
    Task<AuthenticatedUser?> CurrentUser(HttpContext httpContext, CancellationToken cancellationToken);

    Task<ProfileUpdateOutcome> UpdateProfile(
        HttpContext httpContext,
        ProfileChanges changes,
        CancellationToken cancellationToken
    );
}

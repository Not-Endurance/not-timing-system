using Microsoft.AspNetCore.Http;
using NC.Server.Authentication.SignIn;

namespace NC.Server.Authentication;

/// <summary>
/// Sign-in and sign-out, for the module's own routes. A method that reads or writes the request or its cookies takes the
/// HttpContext: the session cookie, the language of an email and the client address are read from it, and the session
/// cookie is written to it. The signed-in user is not asked for here: the routes that need one are guarded by the policy.
/// </summary>
internal interface IAuthentication<TProfile>
    where TProfile : class, new()
{
    /// <summary>
    /// The options a browser needs before it asks an authenticator for a passkey answer. This is the one method without
    /// HttpContext: the ceremony keeps its challenge in a cookie that it writes through the request accessor, so the call
    /// must run inside an HTTP request.
    /// </summary>
    Task<string> PasskeySignInOptions(CancellationToken cancellationToken);

    Task<SignInOutcome<TProfile>> SignInWithPasskey(
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
        RegistrationDetails<TProfile> details,
        CancellationToken cancellationToken
    );

    Task<SignInOutcome<TProfile>> SignInWithCode(
        HttpContext httpContext,
        string email,
        string code,
        CancellationToken cancellationToken
    );

    Task SignOut(HttpContext httpContext, CancellationToken cancellationToken);
}

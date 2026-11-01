#if DEBUG
using System.Net;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Not.Application.Environments;
using Not.Identity;
using NoTiming.Api.Features.Access;
using NoTiming.Api.JsonApi;

namespace NoTiming.Api.Features.Localhost;

/// <summary>
/// "Sign in as" (#607): a developer who runs the whole platform on their machine, against a container database or a hosted
/// staging one through user-secrets, signs in as an account on an allow-list without a code. It exists only in a Debug
/// build (this file is compiled out of a Release one, so no deployed host has it), only in the Development environment,
/// only when a setting names the emails that may be signed in as, and only for a request from the machine itself: from a
/// loopback address, to a loopback host name, and not through a proxy. It works only on a database that says it is Staging or
/// Development: Production, no marker and a marker that says anything else are refused, so that a connection string that
/// points at the wrong database cannot be signed in to by mistake (<c>mark-environment</c> says what a database is). The
/// account has to exist, and nothing is proved of its
/// address: the address stays as unconfirmed as it was and no invitation is taken. What it creates is the session that a
/// sign-in creates.
/// </summary>
internal static class LocalhostSignIn
{
    public const string ALLOW_LIST = "Dev:SignInAs:AllowList";
    public const string PAGE = "dev/sign-in-as";
    public const string SESSIONS = "api/dev/sessions";

    /// <summary>The emails that may be signed in as, in the form accounts are matched by.</summary>
    public static IReadOnlyList<string> AllowListOf(IConfiguration configuration)
    {
        return
        [
            .. (configuration.GetSection(ALLOW_LIST).Get<string[]>() ?? [])
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim().ToLowerInvariant())
                .Distinct(),
        ];
    }

    /// <summary>Whether the setting names somebody to sign in as: without one the host has neither the route nor the page.</summary>
    public static bool IsConfigured(IConfiguration configuration)
    {
        return AllowListOf(configuration).Count > 0;
    }

    /// <summary>Called by <see cref="LocalhostServices"/> and nobody else, so that it only ever runs in Development.</summary>
    public static IServiceCollection AddLocalhostSignIn(this IServiceCollection services, IConfiguration configuration)
    {
        if (IsConfigured(configuration))
        {
            services.AddSingleton(new LocalhostPublicEndpoints(Endpoints));
        }

        return services;
    }

    /// <summary>Called by <see cref="LocalhostServices"/> and nobody else, so that it only ever runs in Development.</summary>
    public static IEndpointRouteBuilder MapLocalhostSignIn(this IEndpointRouteBuilder app, IConfiguration configuration)
    {
        if (!IsConfigured(configuration))
        {
            return app;
        }

        app.MapGet("/" + PAGE, (HttpContext context) => Page(context, configuration));
        app.MapPost("/" + SESSIONS, PostDevSessions);
        return app;
    }

    public static IReadOnlyList<PublicEndpoint> Endpoints { get; } =
    [
        new PublicEndpoint(EndpointAccess.SignIn, "GET,HEAD", PAGE),
        new PublicEndpoint(EndpointAccess.SignIn, "POST", SESSIONS),
    ];

    static async Task<IResult> PostDevSessions(
        HttpContext context,
        IConfiguration configuration,
        UserManager<NIdentityUser> users,
        SignInManager<NIdentityUser> signIn,
        IStorageEnvironment storageEnvironment,
        ILoggerFactory loggers
    )
    {
        if (Refusal(context) is { } notLocal)
        {
            return notLocal;
        }

        var read = await JsonApiRequests.ReadAsync<SignInAsAttributes>(context.Request, "sessions");
        if (read.Error != null)
        {
            return read.Error;
        }

        var marked = await storageEnvironment.ReadAsync(context.RequestAborted);
        if (StorageEnvironmentNames.IsProduction(marked))
        {
            return JsonApiResults.Error(
                StatusCodes.Status403Forbidden,
                "production-database",
                "The database is marked Production.",
                "Nobody is signed in as somebody on a production database."
            );
        }

        if (!StorageEnvironmentNames.IsNonProduction(marked))
        {
            return JsonApiResults.Error(
                StatusCodes.Status409Conflict,
                "environment-not-marked",
                marked == null
                    ? "The database is not marked with the environment it is."
                    : "The database says it is something that is not Staging or Development.",
                "Run mark-environment (or migrate-tenants, or seed-staging) so that it says Staging or Development."
            );
        }

        var email = read.Attributes!.Email?.Trim().ToLowerInvariant();
        if (email == null || !AllowListOf(configuration).Contains(email))
        {
            return JsonApiResults.Error(
                StatusCodes.Status403Forbidden,
                "not-on-the-allow-list",
                "That address is not on the allow-list.",
                $"The emails that may be signed in as are the setting {ALLOW_LIST}."
            );
        }

        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            return JsonApiResults.Error(
                StatusCodes.Status404NotFound,
                "account-not-found",
                "There is no account with that address."
            );
        }

        if (string.IsNullOrEmpty(user.SecurityStamp))
        {
            await users.UpdateSecurityStampAsync(user);
        }

        await signIn.SignInAsync(user, isPersistent: true);
        loggers
            .CreateLogger(typeof(LocalhostSignIn))
            .LogWarning("User {UserId} was signed in as through the local route of a Debug build.", user.Id);
        return JsonApiResults.Resource(
            StatusCodes.Status201Created,
            "sessions",
            "current",
            new
            {
                accountId = user.Id,
                email = user.Email,
                method = "dev",
                passkeyCount = (await users.GetPasskeysAsync(user)).Count,
            },
            "/api/me"
        );
    }

    static IResult Page(HttpContext context, IConfiguration configuration)
    {
        if (Refusal(context) is { } notLocal)
        {
            return notLocal;
        }

        var buttons = string.Join(
            "",
            AllowListOf(configuration)
                .Select(x =>
                    $"<li><button type=\"button\" data-email=\"{HtmlEncoder.Default.Encode(x)}\">{HtmlEncoder.Default.Encode(x)}</button></li>"
                )
        );
        var page = $$"""
            <!doctype html>
            <html lang="en">
            <head><meta charset="utf-8"><title>Sign in as</title></head>
            <body id="sign-in-as-page">
            <h1>Sign in as</h1>
            <p>Local only: a Debug build, on this machine, on a database that is marked as not production.</p>
            <ul>{{buttons}}</ul>
            <p id="answer"></p>
            <script>
            for (const button of document.querySelectorAll('button[data-email]')) {
              button.addEventListener('click', async () => {
                const response = await fetch('/{{SESSIONS}}', {
                  method: 'POST',
                  headers: { 'Content-Type': 'application/vnd.api+json', 'X-Requested-With': 'NoTiming' },
                  body: JSON.stringify({ data: { type: 'sessions', attributes: { email: button.dataset.email } } }),
                });
                if (response.ok) { location.href = '/'; return; }
                const body = await response.json().catch(() => null);
                document.getElementById('answer').textContent = body?.errors?.[0]?.title ?? ('Refused: ' + response.status);
              });
            }
            </script>
            </body>
            </html>
            """;
        return Results.Content(page, "text/html; charset=utf-8");
    }

    /// <summary>The refusal of a request that does not come from this machine, to this machine's own name, directly.</summary>
    static IResult? Refusal(HttpContext context)
    {
        return IsLocal(context)
            ? null
            : JsonApiResults.Error(
                StatusCodes.Status403Forbidden,
                "local-only",
                "This is for the machine it runs on.",
                "The request has to come from a loopback address to a loopback host name, and not through a proxy."
            );
    }

    static bool IsLocal(HttpContext context)
    {
        var headers = context.Request.Headers;
        if (
            headers.ContainsKey("X-Forwarded-For")
            || headers.ContainsKey("X-Forwarded-Host")
            || headers.ContainsKey("Forwarded")
        )
        {
            return false;
        }

        // IsLoopback knows 127.0.0.0/8, ::1 and the form a dual-stack listener reports the loopback address in.
        var remote = context.Connection.RemoteIpAddress;
        if (remote is null)
        {
            return false;
        }

        return IPAddress.IsLoopback(remote) && context.Request.Host.Host is "localhost" or "127.0.0.1" or "[::1]";
    }
}

internal sealed class SignInAsAttributes
{
    public string? Email { get; set; }
}
#endif

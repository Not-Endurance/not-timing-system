using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NTS.Tests.Integration.Infrastructure;

/// <summary>
/// The only way a test signs in (ADR-0011): a scheme the tests register on their own copy of the host, never the host
/// itself. A request names its user in X-Test-User as "email|name". A bearer token is never read, in particular not the
/// old "integration|email|name|scope" token that the deleted test path of the host accepted.
/// </summary>
internal sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SCHEME = "Test";
    public const string USER_HEADER = "X-Test-User";

    public TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder
    )
        : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(USER_HEADER, out var header))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var parts = header.ToString().Split('|', 2);
        var claims = new List<Claim>
        {
            new(ClaimTypes.Email, parts[0]),
            new("name", parts.Length > 1 ? parts[1] : parts[0]),
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SCHEME));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SCHEME)));
    }
}

internal static class TestAuthentication
{
    public const string WHO_AM_I_PATH = "/test/whoami";

    /// <summary>
    /// Registers the scheme and a probe at <see cref="WHO_AM_I_PATH"/> that answers 401 unless the test scheme signs
    /// the caller in. The probe exists only on the copy of the host that asks for it.
    /// </summary>
    public static void AddTestAuthentication(this IServiceCollection services)
    {
        services
            .AddAuthentication(TestAuthenticationHandler.SCHEME)
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                TestAuthenticationHandler.SCHEME,
                _ => { }
            );
        services.AddSingleton<IStartupFilter, WhoAmIStartupFilter>();
    }

    sealed class WhoAmIStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            return app =>
            {
                app.Use(
                    async (context, nextMiddleware) =>
                    {
                        if (context.Request.Path != WHO_AM_I_PATH)
                        {
                            await nextMiddleware();
                            return;
                        }

                        var result = await context.AuthenticateAsync(TestAuthenticationHandler.SCHEME);
                        if (!result.Succeeded)
                        {
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            return;
                        }

                        await context.Response.WriteAsJsonAsync(
                            new { email = result.Principal.FindFirstValue(ClaimTypes.Email) }
                        );
                    }
                );
                next(app);
            };
        }
    }
}

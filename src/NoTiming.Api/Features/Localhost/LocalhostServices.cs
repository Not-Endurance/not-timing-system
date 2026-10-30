using NoTiming.Api.Features.Access;

namespace NoTiming.Api.Features.Localhost;

/// <summary>
/// The one place where what lets a developer past authentication on their own machine is registered, and it is
/// registered only in the Development environment. Everything of the kind lives in this folder and is named
/// <c>Localhost…</c>: it is compiled out of a Release build, so no deployed host has it (<c>LocalhostSignInBuildTests</c>).
/// A new bypass is added here and nowhere else: its services in <see cref="AddLocalhost"/>, its routes in
/// <see cref="MapLocalhost"/>, and whatever it makes public in <see cref="LocalhostPublicEndpoints"/>.
/// <c>LocalhostEmailSender</c>, which prints the codes that sign in asks for, is the one that is not a Debug-only type: it is
/// part of Not.Identity and refuses Production itself, and the sender setting of the host selects it.
/// </summary>
internal static class LocalhostServices
{
    public static IServiceCollection AddLocalhost(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment
    )
    {
        if (!environment.IsDevelopment())
        {
            return services;
        }

#if DEBUG
        services.AddLocalhostSignIn(configuration);
#endif
        return services;
    }

    public static WebApplication MapLocalhost(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
        {
            return app;
        }

#if DEBUG
        app.MapLocalhostSignIn(app.Configuration);
#endif
        return app;
    }

    /// <summary>What the localhost bypass makes public on this host; none in a Release build and in any host that has no bypass.</summary>
    public static IEnumerable<PublicEndpoint>? PublicEndpointsOf(HttpContext context)
    {
#if DEBUG
        return context.RequestServices.GetService<LocalhostPublicEndpoints>()?.Entries;
#else
        return null;
#endif
    }
}

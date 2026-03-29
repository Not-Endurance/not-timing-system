using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Not.Application.Configurations;
using Not.Application.Authentication.User;

namespace Not.Server.Authentication;

public static class ServerAuthenticationServiceCollectionExtensions
{
    public static IServiceCollection NOpenIdAuthentication(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddScoped<NUserResolver>();
        services.AddSettings<NServerAuthenticationSettings>(configuration);

        var settings = GetRequiredSettings(configuration);
        ValidateInteractiveSettings(settings);

        services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie()
            .AddOpenIdConnect(
                OpenIdConnectDefaults.AuthenticationScheme,
                options =>
                {
                    options.Authority = ServerAuthenticationSettingsHelper.ResolveAuthority(settings);
                    options.ClientId = settings.SignInClientId;
                    options.ClientSecret = settings.SignInClientSecret;
                    options.ResponseType = "code";
                    options.SaveTokens = true;
                    options.Events = new OpenIdConnectEvents { OnTicketReceived = ResolveTicketReceived };
                }
            );

        return services;
    }

    public static IServiceCollection NJwtTokenValidation(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddSettings<NServerAuthenticationSettings>(configuration);

        var settings = ServerAuthenticationSettingsHelper.GetSettings(configuration);
        var authenticationBuilder = ServerAuthenticationSettingsHelper.HasTokenValidationConfiguration(settings)
            ? services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            : services.AddAuthentication();

        services.AddAuthorization();
        if (settings == null || !ServerAuthenticationSettingsHelper.HasTokenValidationConfiguration(settings))
        {
            return services;
        }

        var authority = ServerAuthenticationSettingsHelper.ResolveAuthority(settings)
            ?? throw new InvalidOperationException("Missing required token validation authority.");
        var validAudiences = ServerAuthenticationSettingsHelper.ResolveValidAudiences(settings);
        var accessTokenQueryPaths = ServerAuthenticationSettingsHelper.ResolveAccessTokenQueryPaths(settings);

        authenticationBuilder.AddJwtBearer(
            JwtBearerDefaults.AuthenticationScheme,
            options =>
            {
                options.Authority = authority;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = true,
                    ValidAudiences = validAudiences,
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"].ToString();
                        if (string.IsNullOrWhiteSpace(accessToken))
                        {
                            return Task.CompletedTask;
                        }

                        if (
                            accessTokenQueryPaths.Any(x =>
                                context.HttpContext.Request.Path.StartsWithSegments(x)
                            )
                        )
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    },
                };
            }
        );

        return services;
    }

    static async Task ResolveTicketReceived(TicketReceivedContext context)
    {
        var resolver = context.HttpContext.RequestServices.GetRequiredService<NUserResolver>();
        var result = await resolver.ResolvePrincipal(context.Principal);

        if (!result.IsSuccess)
        {
            context.Response.Redirect(result.ServerRedirect);
            context.Fail(result.Error);
            context.HandleResponse();
        }

        context.Principal = result.Principal;
    }

    static NServerAuthenticationSettings GetRequiredSettings(IConfiguration configuration)
    {
        var settings = ServerAuthenticationSettingsHelper.GetSettings(configuration)
            ?? throw new InvalidOperationException(
                $"Missing required authentication section '{nameof(NServerAuthenticationSettings)}'."
            );
        return settings;
    }

    static void ValidateInteractiveSettings(NServerAuthenticationSettings settings)
    {
        RequireConfigValue(settings.SignInClientId, nameof(NServerAuthenticationSettings.SignInClientId));
        RequireConfigValue(settings.SignInClientSecret, nameof(NServerAuthenticationSettings.SignInClientSecret));
        RequireConfigValue(settings.AuthorityInstance, nameof(NServerAuthenticationSettings.AuthorityInstance));
        RequireConfigValue(settings.AuthorityTenantId, nameof(NServerAuthenticationSettings.AuthorityTenantId));
    }

    static string RequireConfigValue(string? value, string settingPath)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        throw new InvalidOperationException($"Missing required authentication setting '{settingPath}'.");
    }
}

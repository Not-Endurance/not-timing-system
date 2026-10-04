using Microsoft.AspNetCore.SignalR;
using NoTiming.Api.Features.Account;
using NoTiming.Api.Features.Live;
using NoTiming.Api.Features.Profile;
using NoTiming.Api.Features.UserSessions;
using NTS.Application.Cors;

namespace NoTiming.Api.Hosting;

internal static class ApiServices
{
    public const string CORS_POLICY_NAME = "NoTimingApiCors";

    public static IServiceCollection AddNoTimingApi(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment
    )
    {
        var originValidator = services.AddNtsCorsOriginValidation(configuration);

        services.AddCors(options =>
            options.AddPolicy(
                CORS_POLICY_NAME,
                policy =>
                    policy
                        .SetIsOriginAllowed(originValidator.IsAllowed)
                        .WithMethods("GET", "POST")
                        .AllowAnyHeader()
                        .AllowCredentials()
            )
        );

        services.AddAccount();
        services.AddSingleton<ProfileStore>();
        services.AddSingleton<UserSessionStore>();

        services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = environment.IsDevelopment();
            options.AddFilter<HubExceptionFilter>();
        });
        services.AddSingleton<IParticipationChanges, ParticipationChanges>();

        return services.AddApiTelemetry(configuration);
    }
}

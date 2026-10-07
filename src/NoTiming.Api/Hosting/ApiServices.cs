using Microsoft.AspNetCore.SignalR;
using NoTiming.Api.Features.Account;
using NoTiming.Api.Features.EventData;
using NoTiming.Api.Features.Events;
using NoTiming.Api.Features.Live;
using NoTiming.Api.Features.Profile;
using NoTiming.Api.Features.Reference;
using NoTiming.Api.Features.Snapshots;
using NoTiming.Api.Features.Tenancy;
using NoTiming.Api.Features.UserSessions;
using NTS;
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

        // The texts of the domain (what is missing of a Setup that cannot start, say) come from the resources of the
        // application, which have to be there for them to be text and not the name of the resource.
        services.AddNts(configuration);

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
#if DEBUG
        services.AddLocalSignIn(configuration, environment);
#endif
        services.AddSingleton<ProfileStore>();
        ApiMongo.Configure();
        services.AddSingleton<TenancyLog>();
        services.AddSingleton<TenantStore>();
        services.AddSingleton<TenantCollections>();
        services.AddSingleton<CrossTenantReads>();
        services.AddSingleton<EventStore>();
        services.AddSingleton<EventStarter>();
        services.AddSingleton<EventGrantStore>();
        services.AddSingleton<GrantInvitations>();
        services.AddHostedService<TenancyIndexes>();
        services.AddSingleton<CallerReader>();
        services.AddSingleton<GlobalCollections>();
        services.AddSingleton<ReferenceAccess>();
        services.AddSingleton<EventDataAccess>();
        services.AddSingleton<SnapshotRecorder>();
        services.AddSingleton<RankingFinaliser>();
        services.AddOptions<RankingFinalisationOptions>().BindConfiguration(RankingFinalisationOptions.SECTION);
        services.AddHostedService<RankingFinalisationSweep>();
        services.AddSingleton<AccountSearch>();
        services
            .AddOptions<SearchRateLimitOptions>()
            .BindConfiguration(SearchRateLimitOptions.SECTION)
            .Validate(
                SearchRateLimitOptions.IsValid,
                "Search:RateLimits: every limit is at least 1 and the window is not zero."
            )
            .ValidateOnStart();
        services.AddSingleton<SearchRateLimiter>();
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

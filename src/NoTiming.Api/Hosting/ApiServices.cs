using Microsoft.AspNetCore.SignalR;
using NoTiming.Api.Features.Account;
using NoTiming.Api.Features.Live;
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

        services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = environment.IsDevelopment();
            options.AddFilter<HubExceptionFilter>();
        });

        return services.AddApiTelemetry(configuration);
    }
}

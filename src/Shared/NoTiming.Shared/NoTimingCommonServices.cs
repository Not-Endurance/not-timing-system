using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NC.Localization;
using NoTiming.Shared.Localization.Resources;

namespace NoTiming.Shared;

public static class NoTimingCommonServices
{
    public static IServiceCollection AddNoTiming(this IServiceCollection services, IConfiguration configuration)
    {
        return services.AddNLocalization<LocalizedStrings>(configuration);
    }
}

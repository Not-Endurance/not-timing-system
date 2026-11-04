using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Localization;
using NTS.Localization.Resources;

namespace NTS;

public static class NtsCommonServices
{
    public static IServiceCollection AddNts(this IServiceCollection services, IConfiguration configuration)
    {
        return services.AddNLocalization<LocalizedStrings>(configuration);
    }
}

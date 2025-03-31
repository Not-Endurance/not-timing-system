using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Localization;
using NTS.Localization.Resources;

namespace NTS;

public static class NtsServiceCollectionExtensions
{
    public static IServiceCollection ConfigureNts(this IServiceCollection services, IConfiguration configuration)
    {
        return services.AddNLocalization<LocalizedStrings>(configuration);
    }
}

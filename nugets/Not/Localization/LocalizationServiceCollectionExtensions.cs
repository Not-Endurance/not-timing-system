using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Not.Localization.Localizers;

namespace Not.Localization;

public static class LocalizationServiceCollectionExtensions
{
    public static IServiceCollection AddNLocalization<T>(this IServiceCollection services, IConfiguration _)
    {
        return services.AddLocalization().AddSingleton<ResxLocalizer<T>>();
    }

    public static IServiceCollection AddDummyLocalizer(this IServiceCollection services)
    {
        return services.AddSingleton<IStringLocalizer, DummyLocalizer>();
    }
}

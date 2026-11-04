using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NC.Localization.Localizers;

namespace NC.Localization;

public static class LocalizationServiceCollectionExtensions
{
    public static IServiceCollection AddNLocalization<T>(this IServiceCollection services, IConfiguration _)
    {
        return services.AddLocalization().AddSingleton<IStringLocalizer, ResxLocalizer<T>>();
    }

    public static IServiceCollection AddDummyLocalizer(this IServiceCollection services)
    {
        return services.AddSingleton<IStringLocalizer, DummyLocalizer>();
    }
}

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace Not.Localization;

public static class LocalizationInjection
{
    public static IServiceCollection AddDummyLocalizer(this IServiceCollection services)
    {
        return services.AddSingleton<IStringLocalizer, DummyLocalizer>();
    }
}

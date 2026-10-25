using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NoTiming.Api.Hosting.Cors;

public static class CorsServiceCollectionExtensions
{
    public static ICorsOriginValidator AddNtsCorsOriginValidation(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var settings = configuration.GetSection(nameof(CorsSettings)).Get<CorsSettings>() ?? new CorsSettings();
        var validator = new CorsOriginValidator(settings);

        services.AddSingleton(settings);
        services.AddSingleton<ICorsOriginValidator>(validator);
        return validator;
    }
}

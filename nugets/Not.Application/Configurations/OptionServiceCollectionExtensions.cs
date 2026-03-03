using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Not.Application.Configurations;

public static class OptionServiceCollectionExtensions
{
    public static IServiceCollection AddSettings<T>(this IServiceCollection services, IConfiguration configuration)
        where T : class
    {
        var section = configuration.GetSection(typeof(T).Name);
        services.AddOptions<T>().Bind(section);
        return services;
    }

    public static IServiceCollection AddSettings<T>(
        this IServiceCollection services,
        IConfiguration configuration,
        Func<T, bool> validator,
        Action<T>? configure = null
    )
        where T : class
    {
        var name = typeof(T).Name;
        var section = configuration.GetSection(name);

        var builder = services.AddOptions<T>().Bind(section);
        if (configure != null)
        {
            builder.Configure(configure);
        }
        builder
            .Bind(section)
            .Validate(validator, $"Section '{name}' is required and missing or invalid")
            .ValidateOnStart();
        return services;
    }
}

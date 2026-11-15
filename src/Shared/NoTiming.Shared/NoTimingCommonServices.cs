using System.Collections.Generic;
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

    /// <summary>The languages the packaged resources have, read from the resource files themselves. English is the default.</summary>
    public static IReadOnlyList<string> SupportedLanguages { get; } =
        ResourceCultures.Discover(typeof(LocalizedStrings).Assembly, typeof(LocalizedStrings).FullName!, "en");
}

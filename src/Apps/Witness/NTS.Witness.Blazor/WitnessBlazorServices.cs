using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace NTS.Witness.Blazor;

public static class WitnessBlazorServices
{
    public static IServiceCollection AddWitnessBlazor(this IServiceCollection services, IConfiguration _)
    {
        return services;
    }
}

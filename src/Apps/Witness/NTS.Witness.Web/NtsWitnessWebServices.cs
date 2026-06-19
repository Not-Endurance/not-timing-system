using System.Reflection;
using NTS.Witness;
using NTS.Witness.Blazor;
using NTS.Storage;

namespace NTS.Witness.Web;

public static class NtsWitnessWebServices
{
    public static IServiceCollection AddNtsWitnessWeb(
        this IServiceCollection services,
        IConfiguration configuration,
        string baseUrl,
        Assembly rootAssembly
    )
    {
        services.ConfigureNtsStorage(configuration).AddRestApiStorage();
        return services.AddNtsWitness(configuration, baseUrl, rootAssembly).AddWitnessBlazor(configuration);
    }
}

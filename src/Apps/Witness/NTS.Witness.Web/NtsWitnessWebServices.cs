using NTS.Witness.Storage;

namespace NTS.Witness.Web;

public static class NtsWitnessWebServices
{
    public static IServiceCollection ConfigureNtsWitnessWeb(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddRazorPages();
        services.AddServerSideBlazor();
        services.ConfigureWitnessStorage(configuration).AddRestApiStorage();
        return services.AddNtsWitness(configuration);
    }
}

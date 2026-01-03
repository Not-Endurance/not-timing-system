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
        return services.ConfigureNtsWitness(configuration);
    }
}

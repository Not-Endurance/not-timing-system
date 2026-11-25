using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NC.Server.ClientIdentity;

public static class NClientIdentityServices
{
    public static IServiceCollection AddNRequestClientIdentity(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.TryAddScoped<IRequestClientIdentity, RequestClientIdentity>();
        return services;
    }
}

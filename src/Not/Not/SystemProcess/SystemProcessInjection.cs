using Microsoft.Extensions.DependencyInjection;

namespace Not.SystemProcess;

public static class SystemProcessInjection
{
    public static IServiceCollection AddProcessTether(this IServiceCollection services, string pid)
    {
        var context = new ProcessTetherContext(pid);
        return services
            .AddSingleton(context)
            .AddHostedService<ProcessTetherLoop>();
    }
}

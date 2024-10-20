﻿using Microsoft.Extensions.DependencyInjection;

namespace NTS.Persistence.Startup;

public static class PersistenceStartup
{
    /// <summary>
    /// Necessary to be called directly from UI project, otherwise the runtime treeshakes this
    /// DLL off, because no resources are explicitly referenced.
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddPersistence(this IServiceCollection services)
    {
        return services;
    }
}

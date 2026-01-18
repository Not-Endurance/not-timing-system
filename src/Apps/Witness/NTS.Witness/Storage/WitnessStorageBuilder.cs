using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Not.Filesystem;
using Not.Storage;

namespace NTS.Witness.Storage;
public static class WitnessStorageServices
{
    // Necessary to be called directly from UI project, otherwise the runtime treeshakes this
    // DLL off, because no resources are explicitly referenced.
    public static Builder ConfigureWitnessStorage(
        this IServiceCollection services,
        IConfiguration configuration,
        string debugRootDirectoryName = "nts"
    )
    {
        FileContextHelper.SetDebugRootDirectory(debugRootDirectoryName);
        return new(services, configuration);
    }

    public class Builder
    {
        readonly NStorageBuilder _nStorageBuilder;
        readonly IServiceCollection _services;

        internal Builder(IServiceCollection services, IConfiguration configuration)
        {
            _nStorageBuilder = new(services, configuration);
            _services = services;
        }

        public Builder AddMongoStorage(string? connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new ApplicationException("MongoDB connection string is null");
            }
            _nStorageBuilder.AddMongoStorage(connectionString);
            return this;
        }

        public Builder AddRestApiStorage()
        {
            _nStorageBuilder.AddRestApiStorage(Assembly.GetExecutingAssembly());
            return this;
        }
    }
}

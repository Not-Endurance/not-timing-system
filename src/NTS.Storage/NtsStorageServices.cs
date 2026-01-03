using Microsoft.Extensions.DependencyInjection;
using Not.Filesystem;

namespace NTS.Storage;

public static class NtsStorageServices
{
    // Necessary to be called directly from UI project, otherwise the runtime treeshakes this
    // DLL off, because no resources are explicitly referenced.
    public static IServiceCollection ConfigureNtsStorage(
        this IServiceCollection services,
        string debugRootDirectoryName = "nts"
    )
    {
        FileContextHelper.SetDebugRootDirectory(debugRootDirectoryName);
        return services.AddJsonFileStore();
    }
}

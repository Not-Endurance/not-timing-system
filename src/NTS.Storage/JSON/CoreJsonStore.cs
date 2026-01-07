using Microsoft.Extensions.DependencyInjection;
using Not.Filesystem;
using Not.Storage.JsonFile.Stores.Files;
using NTS.Judge.Features.Core;
using NTS.Storage.Core;

namespace NTS.Storage.JSON;

internal class CoreJsonStore : LockingJsonFileStore<CoreState>, ICoreState
{
    public CoreJsonStore([FromKeyedServices("NDataKey")] IFileContext configuration) : base(configuration)
    {
    }

    public Task Reset()
    {
        return Delete();
    }
}

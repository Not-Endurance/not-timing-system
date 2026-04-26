using Microsoft.Extensions.DependencyInjection;
using Not.Filesystem;
using Not.Storage.JsonFile.Stores.Files;
using NTS.Application.Contracts.Core;
using NTS.Application.Contracts.Core.Models;
using NTS.Storage.Core;

namespace NTS.Storage.JSON;

internal class CoreJsonStore : LockingJsonFileStore<CoreState>, ICoreState
{
    public CoreJsonStore([FromKeyedServices("NDataKey")] IFilesystemContext configuration)
        : base(configuration) { }

    public Task Reset()
    {
        return Delete();
    }
}

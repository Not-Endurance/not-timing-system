using Microsoft.Extensions.DependencyInjection;
using Not.Exceptions;
using Not.Filesystem;
using NTS.Application.Contracts.Socket;
using NTS.Domain.Core.Objects;

namespace NTS.Judge.Features.Core.Rankings.FeiExport;

public class FeiExportService : IFeiExportService
{
    readonly IFeiExportFeature _feiExport;
    readonly IFilesystemContext _filesystemContext;
    readonly INtsSocketContext _socketContext;

    public FeiExportService(
        INtsSocketContext socketContext,
        IFeiExportFeature feiExport,
        [FromKeyedServices("NDataKey")] IFilesystemContext filesystemContext
    )
    {
        _socketContext = socketContext;
        _feiExport = feiExport;
        _filesystemContext = filesystemContext;
    }

    public async Task Create(Ranklist ranklist)
    {
        var enduranceEvent = GuardHelper.ThrowIfDefault(_socketContext.Event);

        var contents = _feiExport.CreateXmlContent(ranklist, enduranceEvent);
        var path = $"{_filesystemContext.AppDirectory}/fei-export-{ranklist.Name}.xml";
        await FileHelper.WriteAsync(path, contents);
    }
}

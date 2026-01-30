using Microsoft.Extensions.DependencyInjection;
using Not.Application.CRUD.Ports;
using Not.Filesystem;
using Not.Injection;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;

namespace NTS.Judge.Features.Core.Rankings.FeiExport;

public class FeiExportService : IFeiExportService
{
    readonly IRepository<EnduranceEvent> _events;
    readonly IFeiExportFeature _feiExport;
    readonly IFilesystemContext _filesystemContext;

    public FeiExportService(
        IRepository<EnduranceEvent> events,
        IFeiExportFeature feiExport,
        [FromKeyedServices("NDataKey")] IFilesystemContext filesystemContext
    )
    {
        _events = events;
        _feiExport = feiExport;
        _filesystemContext = filesystemContext;
    }

    public async Task Create(Ranklist ranklist)
    {
        var enduranceEvent = await _events.Read(0);
        var contents = _feiExport.CreateXmlContent(ranklist, enduranceEvent!);
        var path = $"{_filesystemContext.AppDirectory}/fei-export-{ranklist.Name}.xml";
        await FileHelper.WriteAsync(path, contents);
    }
}

public interface IFeiExportService : ITransient
{
    Task Create(Ranklist ranklist);
}

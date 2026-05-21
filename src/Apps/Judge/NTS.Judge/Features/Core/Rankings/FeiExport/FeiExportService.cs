using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Objects;

namespace NTS.Judge.Features.Core.Rankings.FeiExport;

public class FeiExportService : IFeiExportService
{
    const string CONTENT_TYPE = "application/xml";

    readonly IFeiExportFeature _feiExport;

    public FeiExportService(IFeiExportFeature feiExport)
    {
        _feiExport = feiExport;
    }

    public FeiExportDocument Create(EventInformation eventInformation, IEnumerable<Ranking> rankings)
    {
        var ranklists = rankings.Select(x => new Ranklist(x)).ToList();
        var content = _feiExport.CreateXmlContent(eventInformation, ranklists);
        return new FeiExportDocument(CreateFileName(eventInformation), content, CONTENT_TYPE);
    }

    static string CreateFileName(EventInformation eventInformation)
    {
        var source = string.IsNullOrWhiteSpace(eventInformation.Name)
            ? eventInformation.Id.ToString()
            : eventInformation.Name;
        var sanitized = new string(source.Select(x => char.IsLetterOrDigit(x) ? x : '-').ToArray()).Trim('-');
        return $"fei-export-{sanitized}.xml";
    }
}

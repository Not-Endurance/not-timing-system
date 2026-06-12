using Not.Injection;
using NTS.Domain.Core.Aggregates;

namespace NTS.Judge.Contracts.Features.Core.Rankings.FeiExport;

public interface IFeiExportService : ITransient
{
    FeiExportDocument Create(EventInformation eventInformation, IEnumerable<Ranking> rankings);
}

public sealed class FeiExportDocument
{
    public FeiExportDocument(string fileName, string content, string contentType)
    {
        FileName = fileName;
        Content = content;
        ContentType = contentType;
    }

    public string FileName { get; }
    public string Content { get; }
    public string ContentType { get; }
}

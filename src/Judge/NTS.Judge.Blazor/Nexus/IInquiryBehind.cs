using Not.Blazor.Ports;
using NTS.Storage.Documents.Archive;
using NTS.Storage.Documents.Archive.Models;

namespace NTS.Judge.Blazor.Nexus;

public interface IInquiryBehind : IObservableBehind
{
    ParticipationDocumentModel? Match { get; }
    IReadOnlyList<ArchiveDocument> Records { get; }
    Task Search(int id);
    Task Search(string term);
}

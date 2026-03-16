namespace NTS.Application.Shared;

public interface IEventScopedDocument : IDocument
{
    int EventId { get; set; }
}

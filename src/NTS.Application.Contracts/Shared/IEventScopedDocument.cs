namespace NTS.Application.Contracts.Shared;

public interface IEventScopedDocument : IDocument
{
    int EventId { get; set; }
}

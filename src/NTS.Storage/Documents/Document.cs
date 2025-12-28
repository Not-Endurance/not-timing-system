using Not.Domain.Aggregates;
using Not.Storage.Tenants;
using Not.Structures;

namespace NTS.Storage.Documents;

public abstract class Document : IDocument
{
    public int Id { get; init; }
    public string TenantId { get; set; } = "nts";
}

// TODO: remove IAggregateRoot once only models are used in repositories and use IDocument in IRepository instead
public interface IDocument : IIdentifiable, ITenantAware, IAggregateRoot
{
}

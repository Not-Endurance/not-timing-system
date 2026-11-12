namespace NoTiming.Application.Contracts.Shared;

// A document as it is stored. DomainVersion is the version of the domain rules the document was written with.
// Zero means the document has none, and such a document is refused when it is loaded.
public abstract class PersistedModel
{
    // The version the domain entities are built with. It must match the default of NC.Domain.Entity.
    public const int CURRENT_DOMAIN_VERSION = 1;

    public int DomainVersion { get; set; }
}

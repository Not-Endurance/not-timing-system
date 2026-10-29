namespace NTS.Contracts.Shared;

/// <summary>
/// A document that counts its writes (ADR-0013): every write of it is made against the version it was read at, and
/// increments it, so a write made from a view that has since moved is told apart from one made from the current view.
/// A document that was never written since the versions came has none, which is 0.
/// </summary>
public interface IVersionedDocument
{
    int Version { get; set; }
}

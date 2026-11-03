namespace NTS.Tests.Unit.Harness;

/// <summary>A resource as the client sends it: its id is the id of the document, and every other member is an attribute.</summary>
public sealed class ResourceSample
{
    public ResourceSample(Guid id, string name, string? note)
    {
        Id = id;
        Name = name;
        Note = note;
    }

    public Guid Id { get; }
    public string Name { get; }
    public string? Note { get; }
}

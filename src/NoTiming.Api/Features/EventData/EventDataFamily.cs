using Not.Domain.Abstractions;
using Not.Krud.Abstractions;
using NoTiming.Api.Features.Reference;
using NTS.Contracts.Shared;
using NTS.Domain.Core.Aggregates;

namespace NoTiming.Api.Features.EventData;

/// <summary>
/// One family of what an Event keeps (#604): the Participations, Rankings, Officials and Handouts it made when it started
/// and works on while it is Live. Each is a name, which is the type and the route of the resource, and a collection; the
/// routes of every family are the same (<see cref="EventDataEndpoints"/>). A row belongs to an Event, and to the Tenant of
/// the Event, which the server stamps and no document names; what a document may name is the members of the model, but for
/// the Event of the row, which a document makes it with and a change leaves as it is, and for the members the family keeps
/// to itself.
/// </summary>
internal sealed class EventDataFamily<TModel, TEntity>
    where TModel : class, IDocument, IEventScoped, IKrudModel<TEntity>, new()
    where TEntity : class, IEntity
{
    /// <summary>
    /// What a resource says about itself besides its attributes: the version of a document that counts its writes, which a
    /// change of it is made against (ADR-0013).
    /// </summary>
    public static object? MetaOf(TModel row)
    {
        return row is IVersionedDocument versioned ? new { version = versioned.Version } : null;
    }

    public EventDataFamily(
        string route,
        string collection,
        IEnumerable<string> hidden,
        bool announcesChanges = false,
        IEnumerable<EventDataReference>? referencedBy = null
    )
    {
        Route = route;
        Collection = collection;
        AnnouncesChanges = announcesChanges;
        ReferencedBy = [.. referencedBy ?? []];
        var kept = hidden.ToArray();
        Made = new ReferenceMembers<TModel>(kept, []);
        Changed = new ReferenceMembers<TModel>(kept, [nameof(IEventScoped.EventId)]);
    }

    /// <summary>The type of the resource and the path of its collection: <c>/api/{Route}</c>.</summary>
    public string Route { get; }

    public string Collection { get; }

    /// <summary>Whether the viewers of the Event are told of every write of a row: they are of a Participation (ADR-0006).</summary>
    public bool AnnouncesChanges { get; }

    /// <summary>Where the rows of other families name the rows of this one: a row that is named is not removed.</summary>
    public IReadOnlyList<EventDataReference> ReferencedBy { get; }

    /// <summary>The members a row is made with, and the ones every read shows: all of the model's but the ones kept to the server.</summary>
    public ReferenceMembers<TModel> Made { get; }

    /// <summary>The members a change may name: the same, but for the Event, which a row does not leave.</summary>
    public ReferenceMembers<TModel> Changed { get; }
}

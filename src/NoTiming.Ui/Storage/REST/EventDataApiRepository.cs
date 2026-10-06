using System.Text.Json;
using System.Text.Json.Nodes;
using Not.Application.CRUD.Ports;
using Not.Application.HTTP;
using Not.Domain.Abstractions;
using Not.Krud.Abstractions;
using Not.Storage.REST;
using NTS.Contracts.Shared;
using NTS.Domain.Core.Aggregates;

namespace NoTiming.Ui.Storage.REST;

/// <summary>
/// What an Event keeps as the Api serves it (#604, ADR-0008): the Participations, Rankings, Officials and Handouts are flat
/// resources with the Event as an attribute, and the Api asks every list of them to start with the Event. A repository that
/// is scoped has the Event the Ui has selected at the head of every list, and one that is not reads what the filter of a
/// call names, as <c>HistoricEventService</c> does for the Event it shows. The Event of a row is written when the row is
/// made and a change leaves it as it is. A resource that counts its writes shows the count in its <c>meta</c>, and a change
/// of it names the one it was read at, so that the Api can refuse a change that was made on what has changed since.
/// </summary>
public abstract class EventDataApiRepository<T, TModel> : JsonApiRepository<T, TModel>
    where T : class, IEntity, IEventScoped
    where TModel : class, IEventScoped, IKrudModel<T>, new()
{
    /// <summary>What the Api keeps of a document for itself, and tells in the meta or not at all.</summary>
    static readonly string[] KEPT_BY_THE_API = ["isDeleted", "deletedVersion", "version"];

    /// <param name="notSent">The members this family keeps to itself, besides the ones of every family.</param>
    protected EventDataApiRepository(
        string collection,
        JsonApiClient client,
        IRepositoryScopeFactory<T>? scopeFactory,
        params string[] notSent
    )
        : base(collection, client, scopeFactory, [.. KEPT_BY_THE_API, .. notSent]) { }

    protected override IReadOnlyCollection<string> NotChanged => ["eventId"];

    protected override JsonObject? ChangeMeta(TModel model)
    {
        return model is IVersionedDocument versioned ? new JsonObject { ["version"] = versioned.Version } : null;
    }

    protected override void ReadMeta(TModel model, JsonElement meta)
    {
        if (
            model is IVersionedDocument versioned
            && meta.TryGetProperty("version", out var version)
            && version.TryGetInt32(out var count)
        )
        {
            versioned.Version = count;
        }
    }
}

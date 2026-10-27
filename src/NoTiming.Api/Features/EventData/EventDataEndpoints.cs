using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Net.Http.Headers;
using Not.Domain.Abstractions;
using Not.Identity;
using Not.Krud.Abstractions;
using NoTiming.Api.Features.Access;
using NoTiming.Api.Features.Events;
using NoTiming.Api.Features.Reference;
using NoTiming.Api.Features.Tenancy;
using NoTiming.Api.JsonApi;
using NTS.Contracts.Shared;
using NTS.Domain.Access;
using NTS.Domain.Core.Aggregates;

namespace NoTiming.Api.Features.EventData;

/// <summary>
/// The routes of a family of what an Event keeps, as the rest-api skill prescribes (#604): <c>GET /api/{type}</c> lists the
/// rows of one Event, with the <c>filter</c> that starts with it, <c>sort</c> and <c>page[...]</c>, and <c>GET /api/{type}/{id}</c>
/// reads one. Anybody reads (ADR-0001): what an Event keeps is what its public views show, and the Tenant of a row is the
/// Tenant of its Event, which is told by the Event and never by the caller. <c>POST</c> makes a row with the id the client
/// made for it, <c>PATCH</c> changes the members it names and <c>DELETE</c> removes it, for a Console Operator of the Event, or
/// Root, while it is Live (ADR-0012), and a row that counts its writes is changed against the version it was read at, which the
/// change names in its <c>meta</c> (ADR-0013). Every write that was stored is announced to the viewers of the Event once. A
/// write declares what it needs and the endpoint filter decides it (#671): the Event is the one the document names, or the one
/// the row belongs to, and the handler is reached by a caller who may run it, with the row the route names found once.
/// </summary>
internal static class EventDataEndpoints
{
    public static void Map<TModel, TEntity>(this IEndpointRouteBuilder app, EventDataFamily<TModel, TEntity> family)
        where TModel : class, IDocument, IEventScoped, IKrudModel<TEntity>, new()
        where TEntity : class, IEntity
    {
        var path = $"/api/{family.Route}";
        app.MapNamedGet(
            path,
            (
                HttpContext context,
                EventStore events,
                TenantCollections tenants,
                TimeEventViewers viewers,
                UserManager<NIdentityUser> users
            ) => ListAsync(family, context, events, tenants, viewers, users)
        );
        app.MapNamedGet(
            path + "/{id}",
            (
                string id,
                HttpContext context,
                CrossTenantReads reads,
                EventStore events,
                TimeEventViewers viewers,
                UserManager<NIdentityUser> users
            ) => ReadAsync(family, id, context, reads, events, viewers, users)
        );
        app.MapNamedPost(
                path,
                [Requires(Capability.RunEvent, PlaceIn.EventInBody)]
                (
                    JsonApiBody<NewRow> body,
                    AccessContext access,
                    EventDataAccess data,
                    CancellationToken cancellationToken
                ) => CreateAsync(family, body, access, data, cancellationToken)
            )
            .WithMetadata(new ResourceAttribute(family.Route));
        app.MapNamedPatch(
                path + "/{id}",
                [Requires(Capability.RunEvent, PlaceIn.EventOfRow)]
                (
                    JsonApiBody<MemberChange> body,
                    AccessContext access,
                    EventDataAccess data,
                    CancellationToken cancellationToken
                ) => ChangeAsync(family, body, access, data, cancellationToken)
            )
            .WithMetadata(new ResourceAttribute(family.Route), new EventRowsOf<TModel>(family.Collection));
        app.MapNamedDelete(
                path + "/{id}",
                [Requires(Capability.RunEvent, PlaceIn.EventOfRow)]
                (AccessContext access, EventDataAccess data, CancellationToken cancellationToken) =>
                    RemoveAsync(family, access, data, cancellationToken)
            )
            .WithMetadata(new EventRowsOf<TModel>(family.Collection));
    }

    static async Task<IResult> ListAsync<TModel, TEntity>(
        EventDataFamily<TModel, TEntity> family,
        HttpContext context,
        EventStore events,
        TenantCollections tenants,
        TimeEventViewers viewers,
        UserManager<NIdentityUser> users
    )
        where TModel : class, IDocument, IEventScoped, IKrudModel<TEntity>, new()
        where TEntity : class, IEntity
    {
        var filter = context.Request.Query["filter"].ToString();

        // A visitor reads what an Event keeps, but does not look a person up in it (ADR-0010).
        if (context.User.Identity?.IsAuthenticated != true && EventFilter.Names(filter, family.PersonMembers))
        {
            return JsonApiResults.NotSignedIn();
        }

        var eventId = EventFilter.EventOf(filter);
        if (eventId is not { } id)
        {
            return EventDataFailures.EventRequired.AsResult();
        }

        var query = ReferenceQuery<TModel>.Read(context.Request, family.Made.Hidden);
        if (query.Refusal != null)
        {
            return query.Refusal;
        }

        var facts = await events.FindAsync(id, context.RequestAborted);
        IReadOnlyList<TModel> rows = [];
        if (facts != null)
        {
            rows = await tenants
                .Of<TModel>(family.Collection, facts.TenantId)
                .ReadAsync(x => x.EventId == id, query.Options, query.Skip, query.Take, context.RequestAborted);
        }

        var page = rows.Take(query.Size).ToList();
        var byId = page.ToDictionary(x => x.Id.ToString());
        var sees =
            facts != null
            && family.ShownTo != null
            && await viewers.MaySeeAsync(context, users, facts, context.RequestAborted);
        VaryByCaller(family, context);
        return JsonApiResults.Page(
            family.Route,
            page.Select(x => (x.Id.ToString(), (object)family.Made.AttributesOf(Shown(family, x, sees)))),
            context.Request,
            query.Size,
            query.Number,
            rows.Count > query.Size,
            itemMeta: key => EventDataFamily<TModel, TEntity>.MetaOf(byId[key])
        );
    }

    static async Task<IResult> ReadAsync<TModel, TEntity>(
        EventDataFamily<TModel, TEntity> family,
        string id,
        HttpContext context,
        CrossTenantReads reads,
        EventStore events,
        TimeEventViewers viewers,
        UserManager<NIdentityUser> users
    )
        where TModel : class, IDocument, IEventScoped, IKrudModel<TEntity>, new()
        where TEntity : class, IEntity
    {
        var row = Guid.TryParse(id, out var rowId)
            ? await reads.FindEventRowAsync<TModel>(family.Collection, rowId, context.RequestAborted)
            : null;
        if (row is null)
        {
            return JsonApiResults.NotFound();
        }

        // Whether the caller is Staff of the Event is looked into only for a caller who is signed in, and only where there is
        // something to hide: a visitor, and a row that hides nothing, cost no more than they did.
        VaryByCaller(family, context);
        var sees = false;
        if (family.ShownTo != null && context.User.Identity?.IsAuthenticated == true)
        {
            var facts = await events.FindAsync(row.EventId, context.RequestAborted);
            sees = facts != null && await viewers.MaySeeAsync(context, users, facts, context.RequestAborted);
        }

        return Resource(family, row, StatusCodes.Status200OK, sees);
    }

    static async Task<IResult> CreateAsync<TModel, TEntity>(
        EventDataFamily<TModel, TEntity> family,
        JsonApiBody<NewRow> body,
        AccessContext access,
        EventDataAccess data,
        CancellationToken cancellationToken
    )
        where TModel : class, IDocument, IEventScoped, IKrudModel<TEntity>, new()
        where TEntity : class, IEntity
    {
        var id = body.Id is null ? Guid.NewGuid() : Guid.Parse(body.Id);
        var row = new TModel();
        if (family.Made.Apply(row, body.Attributes.Members ?? [], []) is { } malformed)
        {
            return malformed;
        }

        // The Event the caller was allowed to run is the Event of the row: a document cannot say one and be let in by another.
        if (row.EventId != access.Event.Id)
        {
            throw new InvalidOperationException("The Event of the row is not the Event the caller was let in to.");
        }

        ReferenceRows.SetId(row, id);
        if (ReferenceRows.Invalid<TModel, TEntity>(row) is { } invalid)
        {
            return invalid;
        }

        var collection = data.Collection(family, access.Event);
        if (await collection.InsertAsync(row, cancellationToken))
        {
            await data.AnnounceAsync(family, row);
            return Resource(family, row, StatusCodes.Status201Created, true, $"/api/{family.Route}/{id}");
        }

        // The same id again is the row that was made, as it is for anything a client makes the id of; the id of a row of
        // another Event, or of another Tenant, is not that, and is not told about.
        var existing = await collection.FindAsync(id, cancellationToken);
        return existing is not null && existing.EventId == row.EventId
            ? Resource(family, existing, StatusCodes.Status200OK, true)
            : JsonApiResults.IdTaken();
    }

    static async Task<IResult> ChangeAsync<TModel, TEntity>(
        EventDataFamily<TModel, TEntity> family,
        JsonApiBody<MemberChange> body,
        AccessContext access,
        EventDataAccess data,
        CancellationToken cancellationToken
    )
        where TModel : class, IDocument, IEventScoped, IKrudModel<TEntity>, new()
        where TEntity : class, IEntity
    {
        // The members are checked against the row they are applied to, which is the one the filter found.
        var row = access.RowOf<TModel>();

        // A row that counts its writes is changed against the version the change was made on, and a change that was made on
        // another one changes nothing: the caller reads the row again and makes the change on what it is now.
        var counted = row as IVersionedDocument;
        var basedOn = 0;
        if (counted != null)
        {
            var (given, unusable) = VersionOf(body.Meta);
            if (unusable != null)
            {
                return unusable;
            }

            if (given != counted.Version)
            {
                return StaleParticipation();
            }

            basedOn = given;
        }

        var named = new List<PropertyInfo>();
        if (family.Changed.Apply(row, body.Attributes.Members ?? [], named) is { } malformed)
        {
            return malformed;
        }

        if (ReferenceRows.Invalid<TModel, TEntity>(row) is { } invalid)
        {
            return invalid;
        }

        var update = family.Changed.UpdateOf(row, named);
        if (update.ElementCount == 0)
        {
            return Resource(family, row, StatusCodes.Status200OK, true);
        }

        var collection = data.Collection(family, access.Event);
        if (counted != null)
        {
            if (!await collection.UpdateAtVersionAsync(row.Id, update, basedOn, cancellationToken))
            {
                return StaleParticipation();
            }

            counted.Version = basedOn + 1;
        }
        else if (!await collection.UpdateAsync(row.Id, update, cancellationToken))
        {
            return JsonApiResults.NotFound();
        }

        await data.AnnounceAsync(family, row);
        return Resource(family, row, StatusCodes.Status200OK, true);
    }

    static async Task<IResult> RemoveAsync<TModel, TEntity>(
        EventDataFamily<TModel, TEntity> family,
        AccessContext access,
        EventDataAccess data,
        CancellationToken cancellationToken
    )
        where TModel : class, IDocument, IEventScoped, IKrudModel<TEntity>, new()
        where TEntity : class, IEntity
    {
        var row = access.RowOf<TModel>();
        if (await data.IsReferencedAsync(family, row, access.Event, cancellationToken))
        {
            return InUse();
        }

        if (!await data.Collection(family, access.Event).DeleteAsync(row.Id, cancellationToken))
        {
            return JsonApiResults.NotFound();
        }

        await data.AnnounceAsync(family, row);
        return Results.NoContent();
    }

    /// <summary>The version a change says it was made on, in the <c>meta</c> of its document, or the answer that refuses it.</summary>
    static (int Version, IResult? Refusal) VersionOf(JsonElement? meta)
    {
        if (
            meta is not { ValueKind: JsonValueKind.Object } document
            || !document.TryGetProperty("version", out var named)
        )
        {
            return (0, VersionRequired());
        }

        if (named.ValueKind == JsonValueKind.Number && named.TryGetInt32(out var version) && version >= 0)
        {
            return (version, null);
        }

        return (0, EventDataFailures.MalformedRequest.AsResult());
    }

    static IResult VersionRequired()
    {
        return EventDataFailures.VersionRequired.AsResult();
    }

    static IResult InUse()
    {
        return EventDataFailures.ParticipationInUse.AsResult();
    }

    static IResult StaleParticipation()
    {
        return EventDataFailures.ParticipationChanged.AsResult();
    }

    /// <summary>A read that answers by the caller says so, so that nothing between the Api and the person keeps one caller's answer for another.</summary>
    static void VaryByCaller<TModel, TEntity>(EventDataFamily<TModel, TEntity> family, HttpContext context)
        where TModel : class, IDocument, IEventScoped, IKrudModel<TEntity>, new()
        where TEntity : class, IEntity
    {
        if (family.ShownTo != null)
        {
            context.Response.Headers.Append(HeaderNames.Vary, HeaderNames.Cookie);
        }
    }

    /// <summary>The row as the caller reads it. A write is answered with the row as Staff read it, because it is a Console Operator or Root that writes.</summary>
    static TModel Shown<TModel, TEntity>(EventDataFamily<TModel, TEntity> family, TModel row, bool seesTimeEvents)
        where TModel : class, IDocument, IEventScoped, IKrudModel<TEntity>, new()
        where TEntity : class, IEntity
    {
        return family.ShownTo is { } shown ? shown(row, seesTimeEvents) : row;
    }

    static IResult Resource<TModel, TEntity>(
        EventDataFamily<TModel, TEntity> family,
        TModel row,
        int status,
        bool seesTimeEvents,
        string? location = null
    )
        where TModel : class, IDocument, IEventScoped, IKrudModel<TEntity>, new()
        where TEntity : class, IEntity
    {
        return JsonApiResults.Resource(
            status,
            family.Route,
            row.Id.ToString(),
            family.Made.AttributesOf(Shown(family, row, seesTimeEvents)),
            location,
            EventDataFamily<TModel, TEntity>.MetaOf(row)
        );
    }
}

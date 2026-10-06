using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Not.Domain.Abstractions;
using Not.Identity;
using Not.Krud.Abstractions;
using NoTiming.Api.Features.Events;
using NoTiming.Api.Features.Reference;
using NoTiming.Api.Features.Tenancy;
using NoTiming.Api.JsonApi;
using NTS.Contracts.Shared;
using NTS.Domain.Core.Aggregates;

namespace NoTiming.Api.Features.EventData;

/// <summary>
/// The routes of a family of what an Event keeps, as the rest-api skill prescribes (#604): <c>GET /api/{type}</c> lists the
/// rows of one Event, with the <c>filter</c> that starts with it, <c>sort</c> and <c>page[...]</c>, and <c>GET /api/{type}/{id}</c>
/// reads one. Anybody reads (ADR-0001): what an Event keeps is what its public views show, and the Tenant of a row is the
/// Tenant of its Event, which is told by the Event and never by the caller. <c>POST</c> makes a row with the id the client
/// made for it, <c>PATCH</c> changes the members it names and <c>DELETE</c> removes it, for the Main Operator of the Event
/// while it is Live (ADR-0012), and a row that counts its writes is changed against the version it was read at, which the
/// change names in its <c>meta</c> (ADR-0013). Every write that was stored is announced to the viewers of the Event once.
/// </summary>
internal static class EventDataEndpoints
{
    public static void Map<TModel, TEntity>(this IEndpointRouteBuilder app, EventDataFamily<TModel, TEntity> family)
        where TModel : class, IDocument, IEventScoped, IKrudModel<TEntity>, new()
        where TEntity : class, IEntity
    {
        var path = $"/api/{family.Route}";
        app.MapGet(
            path,
            (HttpContext context, EventStore events, TenantCollections tenants) =>
                ListAsync(family, context, events, tenants)
        );
        app.MapGet(
            path + "/{id}",
            (string id, HttpContext context, CrossTenantReads reads) => ReadAsync(family, id, context, reads)
        );
        app.MapPost(
                path,
                (HttpContext context, UserManager<NIdentityUser> users, EventDataAccess access) =>
                    CreateAsync(family, context, users, access)
            )
            .RequireAuthorization();
        app.MapPatch(
                path + "/{id}",
                (string id, HttpContext context, UserManager<NIdentityUser> users, EventDataAccess access) =>
                    ChangeAsync(family, id, context, users, access)
            )
            .RequireAuthorization();
        app.MapDelete(
                path + "/{id}",
                (string id, HttpContext context, UserManager<NIdentityUser> users, EventDataAccess access) =>
                    RemoveAsync(family, id, context, users, access)
            )
            .RequireAuthorization();
    }

    static async Task<IResult> ListAsync<TModel, TEntity>(
        EventDataFamily<TModel, TEntity> family,
        HttpContext context,
        EventStore events,
        TenantCollections tenants
    )
        where TModel : class, IDocument, IEventScoped, IKrudModel<TEntity>, new()
        where TEntity : class, IEntity
    {
        var eventId = EventFilter.EventOf(context.Request.Query["filter"].ToString());
        if (eventId is not { } id)
        {
            return JsonApiResults.Error(
                StatusCodes.Status400BadRequest,
                "event-required",
                "Name the Event the rows are of.",
                "Start the filter with the Event and join anything else to it with and: filter=eventId eq <id> and ..."
            );
        }

        var query = ReferenceQuery<TModel>.Read(context.Request, family.Made.Hidden);
        if (query.Refusal != null)
        {
            return query.Refusal;
        }

        var facts = await events.FindAsync(id, context.RequestAborted);
        IReadOnlyList<TModel>? rows = [];
        if (facts != null)
        {
            var collection = tenants.Of<TModel>(family.Collection, facts.TenantId);
            IResult? unsupported;
            (rows, unsupported) = await ReferenceEndpoints.ReadPageAsync(
                () =>
                    collection.ReadAsync(
                        x => x.EventId == id,
                        query.Options,
                        query.Skip,
                        query.Take,
                        context.RequestAborted
                    )
            );
            if (unsupported != null)
            {
                return unsupported;
            }
        }

        var page = rows!.Take(query.Size).ToList();
        var byId = page.ToDictionary(x => x.Id.ToString());
        return JsonApiResults.Collection(
            family.Route,
            page.Select(x => (x.Id.ToString(), (object)family.Made.AttributesOf(x))),
            links: ReferenceEndpoints.Links(context.Request, query, rows!.Count > query.Size),
            meta: new { page = new { size = query.Size, number = query.Number } },
            itemMeta: key => EventDataFamily<TModel, TEntity>.MetaOf(byId[key])
        );
    }

    static async Task<IResult> ReadAsync<TModel, TEntity>(
        EventDataFamily<TModel, TEntity> family,
        string id,
        HttpContext context,
        CrossTenantReads reads
    )
        where TModel : class, IDocument, IEventScoped, IKrudModel<TEntity>, new()
        where TEntity : class, IEntity
    {
        var row = Guid.TryParse(id, out var rowId)
            ? await reads.FindEventRowAsync<TModel>(family.Collection, rowId, context.RequestAborted)
            : null;
        return row is null ? JsonApiResults.NotFound() : Resource(family, row, StatusCodes.Status200OK);
    }

    static async Task<IResult> CreateAsync<TModel, TEntity>(
        EventDataFamily<TModel, TEntity> family,
        HttpContext context,
        UserManager<NIdentityUser> users,
        EventDataAccess access
    )
        where TModel : class, IDocument, IEventScoped, IKrudModel<TEntity>, new()
        where TEntity : class, IEntity
    {
        var read = await JsonApiRequests.ReadAsync<MemberAttributes>(context.Request, family.Route);
        if (read.Error != null)
        {
            return read.Error;
        }

        var user = await users.GetUserAsync(context.User);
        if (user is null)
        {
            return JsonApiResults.NotSignedIn();
        }

        var id = Guid.NewGuid();
        if (read.Id != null && !Guid.TryParse(read.Id, out id))
        {
            return JsonApiResults.InvalidId();
        }

        var row = new TModel();
        if (family.Made.Apply(row, read.Attributes!.Members ?? [], []) is { } malformed)
        {
            return malformed;
        }

        // The Event says whose the row is and who may write it, so the document has to name it.
        if (row.EventId == Guid.Empty)
        {
            return JsonApiResults.Error(
                StatusCodes.Status400BadRequest,
                "event-required",
                "Name the Event the row is of.",
                "The Event is the eventId attribute of the document."
            );
        }

        var (facts, refusal) = await access.OpenAsync(user, row.EventId, context.RequestAborted);
        if (refusal != null)
        {
            return refusal;
        }

        ReferenceEndpoints.SetId(row, id);
        if (ReferenceEndpoints.Invalid<TModel, TEntity>(row) is { } invalid)
        {
            return invalid;
        }

        var collection = access.Collection(family, facts!);
        if (await collection.InsertAsync(row, context.RequestAborted))
        {
            await access.AnnounceAsync(family, row);
            return Resource(family, row, StatusCodes.Status201Created, $"/api/{family.Route}/{id}");
        }

        // The same id again is the row that was made, as it is for anything a client makes the id of; the id of a row of
        // another Event, or of another Tenant, is not that, and is not told about.
        var existing = await collection.FindAsync(id, context.RequestAborted);
        return existing is not null && existing.EventId == row.EventId
            ? Resource(family, existing, StatusCodes.Status200OK)
            : JsonApiResults.IdTaken();
    }

    static async Task<IResult> ChangeAsync<TModel, TEntity>(
        EventDataFamily<TModel, TEntity> family,
        string id,
        HttpContext context,
        UserManager<NIdentityUser> users,
        EventDataAccess access
    )
        where TModel : class, IDocument, IEventScoped, IKrudModel<TEntity>, new()
        where TEntity : class, IEntity
    {
        var read = await JsonApiRequests.ReadAsync<MemberAttributes>(context.Request, family.Route);
        if (read.Error != null)
        {
            return read.Error;
        }

        var user = await users.GetUserAsync(context.User);
        if (user is null)
        {
            return JsonApiResults.NotSignedIn();
        }

        if (!Guid.TryParse(id, out var rowId))
        {
            return JsonApiResults.NotFound();
        }

        if (read.Id != null && read.Id != id)
        {
            return JsonApiResults.IdMismatch();
        }

        var row = await access.FindAsync(family, rowId, context.RequestAborted);
        if (row is null)
        {
            return JsonApiResults.NotFound();
        }

        var (facts, refusal) = await access.OpenAsync(user, row.EventId, context.RequestAborted);
        if (refusal != null)
        {
            return refusal;
        }

        // A row that counts its writes is changed against the version the change was made on, and a change that was made on
        // another one changes nothing: the caller reads the row again and makes the change on what it is now.
        var counted = row as IVersionedDocument;
        var basedOn = 0;
        if (counted != null)
        {
            var (given, unusable) = VersionOf(read.Meta);
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
        if (family.Changed.Apply(row, read.Attributes!.Members ?? [], named) is { } malformed)
        {
            return malformed;
        }

        if (ReferenceEndpoints.Invalid<TModel, TEntity>(row) is { } invalid)
        {
            return invalid;
        }

        var update = family.Changed.UpdateOf(row, named);
        if (update.ElementCount == 0)
        {
            return Resource(family, row, StatusCodes.Status200OK);
        }

        var collection = access.Collection(family, facts!);
        if (counted != null)
        {
            if (!await collection.UpdateAtVersionAsync(rowId, update, basedOn, context.RequestAborted))
            {
                return StaleParticipation();
            }

            counted.Version = basedOn + 1;
        }
        else if (!await collection.UpdateAsync(rowId, update, context.RequestAborted))
        {
            return JsonApiResults.NotFound();
        }

        await access.AnnounceAsync(family, row);
        return Resource(family, row, StatusCodes.Status200OK);
    }

    static async Task<IResult> RemoveAsync<TModel, TEntity>(
        EventDataFamily<TModel, TEntity> family,
        string id,
        HttpContext context,
        UserManager<NIdentityUser> users,
        EventDataAccess access
    )
        where TModel : class, IDocument, IEventScoped, IKrudModel<TEntity>, new()
        where TEntity : class, IEntity
    {
        var user = await users.GetUserAsync(context.User);
        if (user is null)
        {
            return JsonApiResults.NotSignedIn();
        }

        var row = Guid.TryParse(id, out var rowId)
            ? await access.FindAsync(family, rowId, context.RequestAborted)
            : null;
        if (row is null)
        {
            return JsonApiResults.NotFound();
        }

        var (facts, refusal) = await access.OpenAsync(user, row.EventId, context.RequestAborted);
        if (refusal != null)
        {
            return refusal;
        }

        if (await access.IsReferencedAsync(family, row, facts!, context.RequestAborted))
        {
            return InUse();
        }

        if (!await access.Collection(family, facts!).DeleteAsync(rowId, context.RequestAborted))
        {
            return JsonApiResults.NotFound();
        }

        await access.AnnounceAsync(family, row);
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

        return (
            0,
            JsonApiResults.Error(
                StatusCodes.Status400BadRequest,
                "malformed-request",
                "The request is malformed.",
                "The version is a whole number, from 0."
            )
        );
    }

    static IResult VersionRequired()
    {
        return JsonApiResults.Error(
            StatusCodes.Status400BadRequest,
            "version-required",
            "Say which version of the Participation the change is based on.",
            "Send the version it was read at as the version of the meta of the document."
        );
    }

    static IResult InUse()
    {
        return JsonApiResults.Error(
            StatusCodes.Status409Conflict,
            "participation-in-use",
            "A Ranking still counts the Participation, or a Handout is still of it.",
            "Take it out of the Rankings that count it and remove the Handouts that are of it first: what they show could not be composed without it."
        );
    }

    static IResult StaleParticipation()
    {
        return JsonApiResults.Error(
            StatusCodes.Status409Conflict,
            "participation-changed",
            "The Participation changed since it was read.",
            "Read it again and make the change on what it is now."
        );
    }

    static IResult Resource<TModel, TEntity>(
        EventDataFamily<TModel, TEntity> family,
        TModel row,
        int status,
        string? location = null
    )
        where TModel : class, IDocument, IEventScoped, IKrudModel<TEntity>, new()
        where TEntity : class, IEntity
    {
        return JsonApiResults.Resource(
            status,
            family.Route,
            row.Id.ToString(),
            family.Made.AttributesOf(row),
            location,
            EventDataFamily<TModel, TEntity>.MetaOf(row)
        );
    }
}

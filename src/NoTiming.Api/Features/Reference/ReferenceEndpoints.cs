using System.Reflection;
using System.Text.Json;
using MongoDB.Driver.Linq;
using Not.Domain.Abstractions;
using Not.Exceptions;
using Not.Identity;
using Not.Krud.Abstractions;
using NoTiming.Api.Features.Access;
using NoTiming.Api.Features.Tenancy;
using NoTiming.Api.JsonApi;
using NTS.Contracts.Shared;
using NTS.Domain.Access;

namespace NoTiming.Api.Features.Reference;

/// <summary>Who may write a family of reference data.</summary>
internal enum ReferenceWrites
{
    /// <summary>The Tenant's registry: Root, and a Console Operator of an Event of the Tenant that is not yet Historic.</summary>
    Registry = 1,

    /// <summary>The platform's reference data: the Developer.</summary>
    Developer = 2,
}

/// <summary>
/// What keeps a row of a family from being deleted while a Participation refers to it (#635, ADR-0010): the member of the
/// combination of a Participation that holds a copy of the row, and what the Api answers when there is one.
/// </summary>
internal sealed class ReferenceInUse
{
    public ReferenceInUse(string member, string code, string title, string detail)
    {
        Member = member;
        Code = code;
        Title = title;
        Detail = detail;
    }

    /// <summary>The member of the combination that holds the copy: <c>Athlete</c> or <c>Horse</c>.</summary>
    public string Member { get; }

    public string Code { get; }
    public string Title { get; }
    public string Detail { get; }
}

/// <summary>
/// One family of reference data and how its routes behave (#603): the resource's type, which is the name of its
/// route, the collection it is kept in, who may write it, and which members it shows, keeps to itself or owns. The
/// routes of every family are the same: a Tenant's rows are found by their id and by a filter, and only a signed-in
/// account reads them.
/// </summary>
internal sealed class ReferenceFamily<TModel, TEntity>
    where TModel : class, IDocument, IKrudModel<TEntity>, new()
    where TEntity : class, IEntity
{
    public ReferenceFamily(
        string route,
        string collection,
        ReferenceWrites writes,
        ReferenceMembers<TModel> members,
        bool isGlobal = false,
        bool canDelete = true,
        ReferenceInUse? inUse = null
    )
    {
        Route = route;
        Collection = collection;
        Writes = writes;
        Members = members;
        IsGlobal = isGlobal;
        CanDelete = canDelete;
        InUse = inUse;
    }

    /// <summary>The type of the resource and the path of its collection: <c>/api/{Route}</c>.</summary>
    public string Route { get; }

    public string Collection { get; }
    public ReferenceWrites Writes { get; }
    public ReferenceMembers<TModel> Members { get; }

    /// <summary>The rows belong to the platform and not to a Tenant.</summary>
    public bool IsGlobal { get; }

    public bool CanDelete { get; }

    /// <summary>What keeps a row from being deleted while a Participation refers to it, or none for a family whose rows are free to go.</summary>
    public ReferenceInUse? InUse { get; }
}

/// <summary>
/// What the routes of every family of reference data need, in one place: the collection a caller sees (the Tenant's they
/// act in, or the platform's), and whether a Participation refers to a row. Whether the caller may write is not asked here:
/// the endpoint filter decides it from what the family says it needs (#671).
/// </summary>
internal sealed class ReferenceAccess
{
    readonly TenantCollections _tenants;
    readonly GlobalCollections _globals;
    readonly CrossTenantReads _reads;

    public ReferenceAccess(TenantCollections tenants, GlobalCollections globals, CrossTenantReads reads)
    {
        _tenants = tenants;
        _globals = globals;
        _reads = reads;
    }

    public IReferenceCollection<TModel> Open<TModel, TEntity>(
        ReferenceFamily<TModel, TEntity> family,
        NIdentityUser user
    )
        where TModel : class, IDocument, IKrudModel<TEntity>, new()
        where TEntity : class, IEntity
    {
        return family.IsGlobal
            ? _globals.Of<TModel>(family.Collection)
            : _tenants.Of<TModel>(family.Collection, AccountRoles.CurrentTenantOf(user));
    }

    /// <summary>Whether a Participation of any Tenant refers to the row: the one cross-tenant question a delete of a registry row asks.</summary>
    public Task<bool> IsReferencedAsync(ReferenceInUse use, Guid rowId, CancellationToken cancellationToken)
    {
        return _reads.IsReferencedByParticipationAsync(use.Member, rowId, cancellationToken);
    }
}

/// <summary>
/// The routes of a family of reference data, as the rest-api skill prescribes (#603): <c>GET /api/{type}</c> lists with
/// <c>filter</c>, <c>sort</c> and <c>page[...]</c>, <c>GET /api/{type}/{id}</c> reads, <c>POST</c> makes (201 and the
/// <c>Location</c>, with the id the document names, which is how a client that makes its ids does it), <c>PATCH</c>
/// changes the members it names (200 and the resource) and <c>DELETE</c> removes (204). Only a signed-in caller reads; who
/// writes is the policy's, decided by the endpoint filter from what the family says it needs (#671), and what a document may
/// name is the family's, so a row can not be given a Tenant. A row is found among the Tenant's and in no other's, and one that
/// is somebody else's answers as a row that is not there.
/// </summary>
internal static class ReferenceEndpoints
{
    public static void Map<TModel, TEntity>(this IEndpointRouteBuilder app, ReferenceFamily<TModel, TEntity> family)
        where TModel : class, IDocument, IKrudModel<TEntity>, new()
        where TEntity : class, IEntity
    {
        var path = $"/api/{family.Route}";
        var writes = WritesOf(family.Writes);
        app.MapNamedGet(
            path,
            [Requires(Capability.SignedIn)]
            (HttpContext context, AccessContext access, ReferenceAccess data) =>
                ListAsync(family, context, access, data)
        );
        app.MapNamedGet(
            path + "/{id}",
            [Requires(Capability.SignedIn)]
            (string id, AccessContext access, ReferenceAccess data, CancellationToken cancellationToken) =>
                ReadAsync(family, id, access, data, cancellationToken)
        );
        app.MapNamedPost(
                path,
                (
                    JsonApiBody<NewMembers> body,
                    AccessContext access,
                    ReferenceAccess data,
                    CancellationToken cancellationToken
                ) => CreateAsync(family, body, access, data, cancellationToken)
            )
            .WithMetadata(writes, new ResourceAttribute(family.Route));
        app.MapNamedPatch(
                path + "/{id}",
                (
                    string id,
                    JsonApiBody<MemberChange> body,
                    AccessContext access,
                    ReferenceAccess data,
                    CancellationToken cancellationToken
                ) => ChangeAsync(family, id, body, access, data, cancellationToken)
            )
            .WithMetadata(writes, new ResourceAttribute(family.Route));
        if (family.CanDelete)
        {
            app.MapNamedDelete(
                    path + "/{id}",
                    (string id, AccessContext access, ReferenceAccess data, CancellationToken cancellationToken) =>
                        RemoveAsync(family, id, access, data, cancellationToken)
                )
                .WithMetadata(writes);
        }
    }

    /// <summary>What a family needs to be written: the registry of the Tenant the account acts in, or the platform's own data.</summary>
    static RequiresAttribute WritesOf(ReferenceWrites writes)
    {
        return writes == ReferenceWrites.Developer
            ? new RequiresAttribute(Capability.EditCountries)
            : new RequiresAttribute(Capability.EditRegistry, PlaceIn.CurrentTenant);
    }

    static async Task<IResult> ListAsync<TModel, TEntity>(
        ReferenceFamily<TModel, TEntity> family,
        HttpContext context,
        AccessContext access,
        ReferenceAccess data
    )
        where TModel : class, IDocument, IKrudModel<TEntity>, new()
        where TEntity : class, IEntity
    {
        var query = ReferenceQuery<TModel>.Read(context.Request, family.Members.Hidden);
        if (query.Refusal != null)
        {
            return query.Refusal;
        }

        var rows = await data.Open(family, access.User)
            .ReadAsync(query.Options, query.Skip, query.Take, context.RequestAborted);
        return JsonApiResults.Page(
            family.Route,
            rows.Take(query.Size).Select(x => (x.Id.ToString(), (object)family.Members.AttributesOf(x))),
            context.Request,
            query.Size,
            query.Number,
            rows.Count > query.Size
        );
    }

    static async Task<IResult> ReadAsync<TModel, TEntity>(
        ReferenceFamily<TModel, TEntity> family,
        string id,
        AccessContext access,
        ReferenceAccess data,
        CancellationToken cancellationToken
    )
        where TModel : class, IDocument, IKrudModel<TEntity>, new()
        where TEntity : class, IEntity
    {
        var row = Guid.TryParse(id, out var rowId)
            ? await data.Open(family, access.User).FindAsync(rowId, cancellationToken)
            : null;
        return row is null ? JsonApiResults.NotFound() : Resource(family, row, StatusCodes.Status200OK);
    }

    static async Task<IResult> CreateAsync<TModel, TEntity>(
        ReferenceFamily<TModel, TEntity> family,
        JsonApiBody<NewMembers> body,
        AccessContext access,
        ReferenceAccess data,
        CancellationToken cancellationToken
    )
        where TModel : class, IDocument, IKrudModel<TEntity>, new()
        where TEntity : class, IEntity
    {
        var id = body.Id is null ? Guid.NewGuid() : Guid.Parse(body.Id);
        var row = new TModel();
        if (family.Members.Apply(row, body.Attributes.Members ?? [], []) is { } malformed)
        {
            return malformed;
        }

        ReferenceRows.SetId(row, id);
        if (ReferenceRows.Invalid<TModel, TEntity>(row) is { } invalid)
        {
            return invalid;
        }

        var collection = data.Open(family, access.User);
        if (await collection.InsertAsync(row, cancellationToken))
        {
            return Resource(family, row, StatusCodes.Status201Created, $"/api/{family.Route}/{id}");
        }

        // The same id again is the row that was made, as it is for anything a client makes the id of; another Tenant's
        // row with it is not told about.
        var existing = await collection.FindAsync(id, cancellationToken);
        return existing is null ? JsonApiResults.IdTaken() : Resource(family, existing, StatusCodes.Status200OK);
    }

    static async Task<IResult> ChangeAsync<TModel, TEntity>(
        ReferenceFamily<TModel, TEntity> family,
        string id,
        JsonApiBody<MemberChange> body,
        AccessContext access,
        ReferenceAccess data,
        CancellationToken cancellationToken
    )
        where TModel : class, IDocument, IKrudModel<TEntity>, new()
        where TEntity : class, IEntity
    {
        var collection = data.Open(family, access.User);
        var row = Guid.TryParse(id, out var rowId) ? await collection.FindAsync(rowId, cancellationToken) : null;
        if (row is null)
        {
            return JsonApiResults.NotFound();
        }

        var failed = await ReferenceRows.ChangeAsync<TModel, TEntity>(
            family.Members,
            collection,
            row,
            body.Attributes.Members ?? [],
            cancellationToken
        );
        return failed ?? Resource(family, row, StatusCodes.Status200OK);
    }

    static async Task<IResult> RemoveAsync<TModel, TEntity>(
        ReferenceFamily<TModel, TEntity> family,
        string id,
        AccessContext access,
        ReferenceAccess data,
        CancellationToken cancellationToken
    )
        where TModel : class, IDocument, IKrudModel<TEntity>, new()
        where TEntity : class, IEntity
    {
        if (!Guid.TryParse(id, out var rowId))
        {
            return JsonApiResults.NotFound();
        }

        var collection = data.Open(family, access.User);
        if (family.InUse is { } use)
        {
            // a row that is not the Tenant's is not told about, so it is found before it is asked whether it is in use
            if (await collection.FindAsync(rowId, cancellationToken) is null)
            {
                return JsonApiResults.NotFound();
            }

            if (await data.IsReferencedAsync(use, rowId, cancellationToken))
            {
                return JsonApiResults.Error(StatusCodes.Status409Conflict, use.Code, use.Title, use.Detail);
            }
        }

        return await collection.DeleteAsync(rowId, cancellationToken) ? Results.NoContent() : JsonApiResults.NotFound();
    }

    static IResult Resource<TModel, TEntity>(
        ReferenceFamily<TModel, TEntity> family,
        TModel row,
        int status,
        string? location = null
    )
        where TModel : class, IDocument, IKrudModel<TEntity>, new()
        where TEntity : class, IEntity
    {
        return JsonApiResults.Resource(
            status,
            family.Route,
            row.Id.ToString(),
            family.Members.AttributesOf(row),
            location
        );
    }
}

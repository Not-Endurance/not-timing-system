using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
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
    /// <summary>The Tenant's registry: a Tenant Root, and the Main Operator of an Event of the Tenant that is not yet Historic.</summary>
    Registry = 1,

    /// <summary>The platform's reference data: the Developer.</summary>
    Developer = 2,
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
        bool canDelete = true
    )
    {
        Route = route;
        Collection = collection;
        Writes = writes;
        Members = members;
        IsGlobal = isGlobal;
        CanDelete = canDelete;
    }

    /// <summary>The type of the resource and the path of its collection: <c>/api/{Route}</c>.</summary>
    public string Route { get; }

    public string Collection { get; }
    public ReferenceWrites Writes { get; }
    public ReferenceMembers<TModel> Members { get; }

    /// <summary>The rows belong to the platform and not to a Tenant.</summary>
    public bool IsGlobal { get; }

    public bool CanDelete { get; }
}

/// <summary>
/// What the routes of every family of reference data need, in one place: the collection a caller sees (the Tenant's they
/// act in, or the platform's), and the policy's answer to whether the caller may write it.
/// </summary>
internal sealed class ReferenceAccess
{
    readonly TenantCollections _tenants;
    readonly GlobalCollections _globals;
    readonly TenantStore _tenantStore;
    readonly CallerReader _callers;

    public ReferenceAccess(
        TenantCollections tenants,
        GlobalCollections globals,
        TenantStore tenantStore,
        CallerReader callers
    )
    {
        _tenants = tenants;
        _globals = globals;
        _tenantStore = tenantStore;
        _callers = callers;
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

    /// <summary>Null when the caller may write the family, and the answer that refuses it when not.</summary>
    public async Task<IResult?> RefuseWriteAsync<TModel, TEntity>(
        ReferenceFamily<TModel, TEntity> family,
        NIdentityUser user,
        CancellationToken cancellationToken
    )
        where TModel : class, IDocument, IKrudModel<TEntity>, new()
        where TEntity : class, IEntity
    {
        if (family.Writes == ReferenceWrites.Developer)
        {
            var verdict = AccessPolicy.Decide(
                Capability.EditCountries,
                AccountRoles.CallerOf(user),
                AccessScope.Platform
            );
            return verdict.IsAllowed ? null : AccessResults.Refused(verdict);
        }

        var tenantId = AccountRoles.CurrentTenantOf(user);
        if (tenantId == null || await _tenantStore.FindAsync(tenantId, cancellationToken) is null)
        {
            return JsonApiResults.Error(
                StatusCodes.Status409Conflict,
                "no-current-tenant",
                "Choose a country on your profile: what you write belongs to the Tenant of one."
            );
        }

        var caller = await _callers.ReadAsync(user, cancellationToken);
        var scope = AccessScope.ForTenant(tenantId, await _tenantStore.IsOperationalAsync(tenantId, cancellationToken));
        var allowed = AccessPolicy.Decide(Capability.EditRegistry, caller, scope);
        return allowed.IsAllowed ? null : AccessResults.Refused(allowed);
    }
}

/// <summary>
/// The routes of a family of reference data, as the rest-api skill prescribes (#603): <c>GET /api/{type}</c> lists with
/// <c>filter</c>, <c>sort</c> and <c>page[...]</c>, <c>GET /api/{type}/{id}</c> reads, <c>POST</c> makes (201 and the
/// <c>Location</c>, with the id the document names, which is how a client that makes its ids does it), <c>PATCH</c>
/// changes the members it names (200 and the resource) and <c>DELETE</c> removes (204). Only a signed-in caller reads; who
/// writes is the policy's, and what a document may name is the family's, so a row can not be given a Tenant. A row is
/// found among the Tenant's and in no other's, and one that is somebody else's answers as a row that is not there.
/// </summary>
internal static class ReferenceEndpoints
{
    public static void Map<TModel, TEntity>(this IEndpointRouteBuilder app, ReferenceFamily<TModel, TEntity> family)
        where TModel : class, IDocument, IKrudModel<TEntity>, new()
        where TEntity : class, IEntity
    {
        var path = $"/api/{family.Route}";
        app.MapGet(
                path,
                (HttpContext context, UserManager<NIdentityUser> users, ReferenceAccess access) =>
                    ListAsync(family, context, users, access)
            )
            .RequireAuthorization();
        app.MapGet(
                path + "/{id}",
                (string id, HttpContext context, UserManager<NIdentityUser> users, ReferenceAccess access) =>
                    ReadAsync(family, id, context, users, access)
            )
            .RequireAuthorization();
        app.MapPost(
                path,
                (HttpContext context, UserManager<NIdentityUser> users, ReferenceAccess access) =>
                    CreateAsync(family, context, users, access)
            )
            .RequireAuthorization();
        app.MapPatch(
                path + "/{id}",
                (string id, HttpContext context, UserManager<NIdentityUser> users, ReferenceAccess access) =>
                    ChangeAsync(family, id, context, users, access)
            )
            .RequireAuthorization();
        if (family.CanDelete)
        {
            app.MapDelete(
                    path + "/{id}",
                    (string id, HttpContext context, UserManager<NIdentityUser> users, ReferenceAccess access) =>
                        RemoveAsync(family, id, context, users, access)
                )
                .RequireAuthorization();
        }
    }

    /// <summary>
    /// The refusal of a row the domain does not accept: it is made into the entity it stands for, which is where its rules
    /// are, and the first one it breaks is 422 with the member it is about.
    /// </summary>
    public static IResult? Invalid<TModel, TEntity>(TModel row)
        where TModel : class, IDocument, IKrudModel<TEntity>, new()
        where TEntity : class, IEntity
    {
        try
        {
            row.MapToEntity();
            return null;
        }
        catch (ValidationException ex)
        {
            return JsonApiResults.Error(
                StatusCodes.Status422UnprocessableEntity,
                "invalid-attribute",
                "A member of the resource is not valid.",
                ex.Message
            );
        }
        catch (Exception ex) when (ex is ArgumentException or NullReferenceException or InvalidOperationException)
        {
            return JsonApiResults.Error(
                StatusCodes.Status422UnprocessableEntity,
                "invalid-attribute",
                "A member of the resource is not valid.",
                "The document is missing a member it needs."
            );
        }
    }

    /// <summary>
    /// What a change of a row is, once the caller may make it, for every family and for the Setup: the members the document
    /// names are put into the row, the row has to be one the domain accepts, and then the members that were named, and no
    /// others, are written. Null when the row was changed, and the answer that refuses it when not.
    /// </summary>
    public static async Task<IResult?> ChangeRowAsync<TModel, TEntity>(
        ReferenceMembers<TModel> members,
        IReferenceCollection<TModel> collection,
        TModel row,
        IReadOnlyDictionary<string, JsonElement> attributes,
        CancellationToken cancellationToken
    )
        where TModel : class, IDocument, IKrudModel<TEntity>, new()
        where TEntity : class, IEntity
    {
        var named = new List<PropertyInfo>();
        if (members.Apply(row, attributes, named) is { } malformed)
        {
            return malformed;
        }

        if (Invalid<TModel, TEntity>(row) is { } invalid)
        {
            return invalid;
        }

        var update = members.UpdateOf(row, named);
        return update.ElementCount > 0 && !await collection.UpdateAsync(row.Id, update, cancellationToken)
            ? JsonApiResults.NotFound()
            : null;
    }

    /// <summary>
    /// Reads a page of a list. An expression that is valid in the OData grammar and that the data cannot run, such as the
    /// date parts of a date (<c>year(endDay)</c>), is for the caller to correct: 400 <c>invalid-filter</c>, and not a
    /// server error.
    /// </summary>
    public static async Task<(IReadOnlyList<T>? Rows, IResult? Refusal)> ReadPageAsync<T>(
        Func<Task<IReadOnlyList<T>>> read
    )
    {
        try
        {
            return (await read(), null);
        }
        catch (ExpressionNotSupportedException)
        {
            return (
                null,
                JsonApiResults.Error(
                    StatusCodes.Status400BadRequest,
                    "invalid-filter",
                    "The filter cannot be applied.",
                    "The expression is valid, but the data cannot run it: write the comparison on the member itself."
                )
            );
        }
    }

    public static object Links<T>(HttpRequest request, ReferenceQuery<T> query, bool hasNext)
        where T : class
    {
        var self = request.Path + request.QueryString;
        return new { self, next = hasNext ? WithPage(request, query.Number + 1) : null };
    }

    static async Task<IResult> ListAsync<TModel, TEntity>(
        ReferenceFamily<TModel, TEntity> family,
        HttpContext context,
        UserManager<NIdentityUser> users,
        ReferenceAccess access
    )
        where TModel : class, IDocument, IKrudModel<TEntity>, new()
        where TEntity : class, IEntity
    {
        var user = await users.GetUserAsync(context.User);
        if (user is null)
        {
            return JsonApiResults.NotSignedIn();
        }

        var query = ReferenceQuery<TModel>.Read(context.Request, family.Members.Hidden);
        if (query.Refusal != null)
        {
            return query.Refusal;
        }

        var (rows, unsupported) = await ReadPageAsync(
            () => access.Open(family, user).ReadAsync(query.Options, query.Skip, query.Take, context.RequestAborted)
        );
        if (unsupported != null)
        {
            return unsupported;
        }

        var page = rows!.Take(query.Size).ToList();
        return JsonApiResults.Collection(
            family.Route,
            page.Select(x => (x.Id.ToString(), (object)family.Members.AttributesOf(x))),
            links: Links(context.Request, query, rows!.Count > query.Size),
            meta: new { page = new { size = query.Size, number = query.Number } }
        );
    }

    static async Task<IResult> ReadAsync<TModel, TEntity>(
        ReferenceFamily<TModel, TEntity> family,
        string id,
        HttpContext context,
        UserManager<NIdentityUser> users,
        ReferenceAccess access
    )
        where TModel : class, IDocument, IKrudModel<TEntity>, new()
        where TEntity : class, IEntity
    {
        var user = await users.GetUserAsync(context.User);
        if (user is null)
        {
            return JsonApiResults.NotSignedIn();
        }

        var row = Guid.TryParse(id, out var rowId)
            ? await access.Open(family, user).FindAsync(rowId, context.RequestAborted)
            : null;
        return row is null ? JsonApiResults.NotFound() : Resource(family, row, StatusCodes.Status200OK);
    }

    static async Task<IResult> CreateAsync<TModel, TEntity>(
        ReferenceFamily<TModel, TEntity> family,
        HttpContext context,
        UserManager<NIdentityUser> users,
        ReferenceAccess access
    )
        where TModel : class, IDocument, IKrudModel<TEntity>, new()
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

        if (await access.RefuseWriteAsync(family, user, context.RequestAborted) is { } refusal)
        {
            return refusal;
        }

        var id = Guid.NewGuid();
        if (read.Id != null && !Guid.TryParse(read.Id, out id))
        {
            return JsonApiResults.InvalidId();
        }

        var row = new TModel();
        if (family.Members.Apply(row, read.Attributes!.Members ?? [], []) is { } malformed)
        {
            return malformed;
        }

        SetId(row, id);
        if (Invalid<TModel, TEntity>(row) is { } invalid)
        {
            return invalid;
        }

        var collection = access.Open(family, user);
        if (await collection.InsertAsync(row, context.RequestAborted))
        {
            return Resource(family, row, StatusCodes.Status201Created, $"/api/{family.Route}/{id}");
        }

        // The same id again is the row that was made, as it is for anything a client makes the id of; another Tenant's
        // row with it is not told about.
        var existing = await collection.FindAsync(id, context.RequestAborted);
        return existing is null ? JsonApiResults.IdTaken() : Resource(family, existing, StatusCodes.Status200OK);
    }

    static async Task<IResult> ChangeAsync<TModel, TEntity>(
        ReferenceFamily<TModel, TEntity> family,
        string id,
        HttpContext context,
        UserManager<NIdentityUser> users,
        ReferenceAccess access
    )
        where TModel : class, IDocument, IKrudModel<TEntity>, new()
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

        if (await access.RefuseWriteAsync(family, user, context.RequestAborted) is { } refusal)
        {
            return refusal;
        }

        if (!Guid.TryParse(id, out var rowId))
        {
            return JsonApiResults.NotFound();
        }

        if (read.Id != null && read.Id != id)
        {
            return JsonApiResults.IdMismatch();
        }

        var collection = access.Open(family, user);
        var row = await collection.FindAsync(rowId, context.RequestAborted);
        if (row is null)
        {
            return JsonApiResults.NotFound();
        }

        var failed = await ChangeRowAsync<TModel, TEntity>(
            family.Members,
            collection,
            row,
            read.Attributes!.Members ?? [],
            context.RequestAborted
        );
        return failed ?? Resource(family, row, StatusCodes.Status200OK);
    }

    static async Task<IResult> RemoveAsync<TModel, TEntity>(
        ReferenceFamily<TModel, TEntity> family,
        string id,
        HttpContext context,
        UserManager<NIdentityUser> users,
        ReferenceAccess access
    )
        where TModel : class, IDocument, IKrudModel<TEntity>, new()
        where TEntity : class, IEntity
    {
        var user = await users.GetUserAsync(context.User);
        if (user is null)
        {
            return JsonApiResults.NotSignedIn();
        }

        if (await access.RefuseWriteAsync(family, user, context.RequestAborted) is { } refusal)
        {
            return refusal;
        }

        var removed =
            Guid.TryParse(id, out var rowId)
            && await access.Open(family, user).DeleteAsync(rowId, context.RequestAborted);
        return removed ? Results.NoContent() : JsonApiResults.NotFound();
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

    static void SetId<TModel>(TModel row, Guid id)
        where TModel : class
    {
        typeof(TModel).GetProperty("Id")!.SetValue(row, id);
    }

    static string WithPage(HttpRequest request, int number)
    {
        var parameters = request
            .Query.Where(x => x.Key != "page[number]")
            .SelectMany(x =>
                x.Value.Select(value => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(value ?? "")}")
            )
            .Append($"{Uri.EscapeDataString("page[number]")}={number}");
        return $"{request.Path}?{string.Join('&', parameters)}";
    }
}

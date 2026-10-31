using System.Linq.Expressions;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Not.Application.CRUD.Ports;
using Not.Application.HTTP;
using Not.Domain.Abstractions;
using Not.Krud.Abstractions;
using Not.Localization;

namespace Not.Blazor.Client.REST;

/// <summary>
/// The repository of a resource that the Api serves as JSON:API documents (ADR-0008, ADR-0017). It maps the model of the
/// resource onto the verbs of <see cref="JsonApiClient"/>, and it throws a <see cref="RepositoryException"/> for every request
/// the Api did not take: a refusal is a <see cref="JsonApiException"/> with the answer, and a request that got no answer is
/// unavailable. A row that is not there is not a failure: a read answers none, and removing a row that is not there is done.
/// </summary>
public abstract class JsonApiRepository<T, TModel> : IRepository<T>
    where T : class, IEntity
    where TModel : class, IKrudModel<T>, new()
{
    const int PAGE_SIZE = 500;
    const int MAX_PAGES = 1000;
    static readonly PropertyInfo ID = typeof(TModel).GetProperty("Id")!;

    readonly IRepositoryScopeFactory<T>? _scopeFactory;

    /// <param name="collection">The type of the resource and the path of its collection, such as <c>clubs</c>.</param>
    /// <param name="scopeFactory">What every list is limited to, such as the Event whose rows these are.</param>
    protected JsonApiRepository(string collection, JsonApiClient client, IRepositoryScopeFactory<T>? scopeFactory = null)
    {
        Collection = collection;
        Client = client;
        _scopeFactory = scopeFactory;
    }

    protected JsonApiClient Client { get; }
    protected string Collection { get; }

    /// <summary>
    /// The members that a change leaves out, because the Api refuses a change that names them: the Event of a row (ADR-0012).
    /// </summary>
    protected virtual IReadOnlyCollection<string> NotChanged => [];

    /// <summary>What a change says of the resource besides its members, in its <c>meta</c>: the version it is based on.</summary>
    protected virtual JsonObject? ChangeMeta(TModel model)
    {
        return null;
    }

    /// <summary>What the Api told of a resource besides its attributes, in its <c>meta</c>, put into the model that was read.</summary>
    protected virtual void ReadMeta(TModel model, JsonElement meta) { }

    protected virtual TModel MapModel(T item)
    {
        var model = new TModel();
        model.MapFrom(item);
        return model;
    }

    /// <summary>
    /// Every resource of a named view of the collection, read a page at a time, in the order asked for (<c>-endDay</c> is the
    /// last day first): <c>events/live</c> is the view <c>live</c> of <c>events</c>.
    /// </summary>
    protected Task<IEnumerable<T>> ReadView(string view, string? sort = null)
    {
        return ReadPages($"{Collection}/{view}", new ListQuery(sort: sort));
    }

    /// <summary>The resource of a document the Api answered with, as the entity it is.</summary>
    protected T? EntityOf(JsonElement? resource)
    {
        if (resource is not { ValueKind: JsonValueKind.Object } data)
        {
            return null;
        }

        var model = data.TryGetProperty("attributes", out var attributes)
            ? JsonSerializer.Deserialize<TModel>(attributes.GetRawText(), Client.Json)
            : new TModel();
        if (model == null)
        {
            return null;
        }

        if (data.TryGetProperty("id", out var id) && Guid.TryParse(id.GetString(), out var parsed))
        {
            ID.SetValue(model, parsed);
        }

        if (data.TryGetProperty("meta", out var meta) && meta.ValueKind == JsonValueKind.Object)
        {
            ReadMeta(model, meta);
        }

        return model.MapToEntity();
    }

    /// <summary>
    /// The answer of a request the Api took. A refusal is thrown with what the Api said, and a request that got no answer is
    /// unavailable.
    /// </summary>
    protected async Task<JsonApiResponse> Taken(Task<JsonApiResponse> request)
    {
        var response = await Reached(request);
        return response.IsSuccess ? response : throw new JsonApiException(response, Client.UnavailableCodes);
    }

    /// <summary>The answer of a request, whatever it says; a request that got no answer is unavailable.</summary>
    protected static async Task<JsonApiResponse> Reached(Task<JsonApiResponse> request)
    {
        try
        {
            return await request;
        }
        catch (HttpRequestException ex)
        {
            throw new RepositoryException(
                RepositoryErrorKind.Unavailable,
                NStrings.Cannot_connect_to_server_string,
                null,
                ex
            );
        }
    }

    public virtual async Task Create(T item)
    {
        await Taken(Client.Post(Collection, MapModel(item)));
    }

    public virtual async Task Update(T item)
    {
        var model = MapModel(item);
        await Taken(Client.Patch(Collection, item.Id, ChangedMembers(model), ChangeMeta(model)));
    }

    public virtual async Task Delete(Guid id)
    {
        var response = await Reached(Client.Delete(Collection, id));

        // A row that is not there is what the caller wanted it to be.
        if (!response.IsSuccess && !response.IsNotFound)
        {
            throw new JsonApiException(response, Client.UnavailableCodes);
        }
    }

    public virtual async Task Delete(T item)
    {
        await Delete(item.Id);
    }

    public virtual async Task DeleteMany(Expression<Func<T, bool>> filter)
    {
        await DeleteMany(await ReadMany(filter));
    }

    public virtual async Task DeleteMany(IEnumerable<T> items)
    {
        // The Api removes one resource at a time, as JSON:API has no request that removes many.
        foreach (var item in items.ToArray())
        {
            await Delete(item.Id);
        }
    }

    public virtual async Task<T?> Read(Expression<Func<T, bool>> filter)
    {
        return (await ReadMany(filter)).FirstOrDefault();
    }

    /// <summary>One row by its id: none when the Api has no such row, and an exception when it did not say.</summary>
    public virtual async Task<T?> Read(Guid id)
    {
        var response = await Reached(Client.GetOne(Collection, id));
        if (response.IsNotFound)
        {
            return null;
        }

        if (!response.IsSuccess)
        {
            throw new JsonApiException(response, Client.UnavailableCodes);
        }

        return EntityOf(response.Document?.GetProperty("data"));
    }

    public virtual async Task<IEnumerable<T>> ReadMany()
    {
        return await ReadWithin(null);
    }

    public virtual async Task<IEnumerable<T>> ReadMany(Expression<Func<T, bool>> filter)
    {
        return await ReadWithin(filter);
    }

    /// <summary>The members a change sends: those of the model, without the ones the Api refuses a change to name.</summary>
    JsonObject ChangedMembers(TModel model)
    {
        var members = JsonSerializer.SerializeToNode(model, Client.Json)!.AsObject();
        foreach (var name in NotChanged)
        {
            members.Remove(name);
        }

        return members;
    }


    async Task<IEnumerable<T>> ReadWithin(Expression<Func<T, bool>>? filter)
    {
        var scope = _scopeFactory?.Create().Filter;
        var filters = Filters(scope, filter);
        if (!ODataApiFilterAdapter.TryParseFilters(filters, out var parameters, camelCase: true))
        {
            throw new RepositoryException(
                RepositoryErrorKind.Refused,
                "The filter of this list cannot be written for the Api (ADR-0017)."
            );
        }

        return await ReadPages(Collection, new ListQuery(filter: parameters.GetValueOrDefault("$filter")));
    }

    static Expression<Func<T, bool>>[] Filters(Expression<Func<T, bool>>? scope, Expression<Func<T, bool>>? filter)
    {
        return [.. new[] { scope, filter }.OfType<Expression<Func<T, bool>>>()];
    }

    /// <summary>
    /// Every row of a list, a page at a time, until the Api says there is no next page. A list that is still going on after
    /// the most pages the repository reads is not read to its end, and is refused rather than cut short.
    /// </summary>
    async Task<IEnumerable<T>> ReadPages(string path, ListQuery query)
    {
        var items = new List<T>();
        var paged = new ListQuery(query.Filter, query.Sort, PAGE_SIZE);
        for (var number = 1; number <= MAX_PAGES; number++)
        {
            var response = await Taken(Client.Get(path, paged.OnPage(number)));
            var rows = response.Document!.Value.GetProperty("data").EnumerateArray();
            items.AddRange(rows.Select(x => EntityOf(x)).OfType<T>());
            if (!response.HasNextPage)
            {
                return items;
            }
        }

        throw new RepositoryException(
            RepositoryErrorKind.Refused,
            $"The list is longer than the {MAX_PAGES} pages the repository reads, and is not read to its end."
        );
    }
}

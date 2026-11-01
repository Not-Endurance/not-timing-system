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
    protected async Task<IEnumerable<T>> ReadView(string view, string? sort = null)
    {
        return (await ResourcesOf($"{Collection}/{view}", new ListQuery(sort: sort))).Select(x => EntityOf(x)).OfType<T>();
    }

    /// <summary>
    /// One page of a route of the collection that answers with a list of rows, such as <c>athletes/all-tenants</c>: the rows,
    /// and whether another page follows.
    /// </summary>
    protected async Task<(IReadOnlyList<JsonElement> Rows, bool HasNext)> PageOf(
        string route,
        ListQuery query,
        CancellationToken cancellationToken = default
    )
    {
        var response = await Taken(Client.Get(route, query, cancellationToken));
        return ([.. response.Document!.Value.GetProperty("data").EnumerateArray()], response.HasNextPage);
    }

    /// <summary>
    /// The rows of the collection that are <paramref name="filter"/> within the scope, in the order asked for, as the models
    /// that the Api answers with: the members that the entity does not hold (such as the name of an account) are read too.
    /// </summary>
    protected async Task<IReadOnlyList<TModel>> ModelsWithin(
        Expression<Func<T, bool>>? filter = null,
        string? sort = null
    )
    {
        var resources = await ResourcesWithin(filter, sort);
        return [.. resources.Select(x => ModelOf(x)).OfType<TModel>()];
    }

    /// <summary>The resource of a document the Api answered with, as the entity it is.</summary>
    protected T? EntityOf(JsonElement? resource)
    {
        return ModelOf(resource)?.MapToEntity();
    }

    /// <summary>The resource of a document the Api answered with, as the model it is: its attributes, its id and its meta.</summary>
    protected TModel? ModelOf(JsonElement? resource)
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

        return model;
    }

    /// <summary>The <c>data</c> of an answer that succeeded: the resource it is about.</summary>
    protected static JsonElement ResourceOf(JsonApiResponse response)
    {
        return response.Document!.Value.GetProperty("data");
    }

    /// <summary>
    /// The answer of a request the Api took. A refusal is thrown with what the Api said, and a request that got no answer is
    /// unavailable.
    /// </summary>
    protected Task<JsonApiResponse> Taken(Task<JsonApiResponse> request)
    {
        return JsonApiAnswers.Taken(request, Client.UnavailableCodes);
    }

    /// <summary>The answer of a request, whatever it says; a request that got no answer is unavailable.</summary>
    protected static Task<JsonApiResponse> Reached(Task<JsonApiResponse> request)
    {
        return JsonApiAnswers.Reached(request);
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

        return EntityOf(ResourceOf(response));
    }

    public virtual async Task<IEnumerable<T>> ReadMany()
    {
        return (await ResourcesWithin(null, null)).Select(x => EntityOf(x)).OfType<T>();
    }

    public virtual async Task<IEnumerable<T>> ReadMany(Expression<Func<T, bool>> filter)
    {
        return (await ResourcesWithin(filter, null)).Select(x => EntityOf(x)).OfType<T>();
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

    async Task<IReadOnlyList<JsonElement>> ResourcesWithin(Expression<Func<T, bool>>? filter, string? sort)
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

        return await ResourcesOf(Collection, new ListQuery(parameters.GetValueOrDefault("$filter"), sort));
    }

    static Expression<Func<T, bool>>[] Filters(Expression<Func<T, bool>>? scope, Expression<Func<T, bool>>? filter)
    {
        return [.. new[] { scope, filter }.OfType<Expression<Func<T, bool>>>()];
    }

    /// <summary>
    /// Every resource of a list, a page at a time, until the Api says there is no next page. A list that is still going on
    /// after the most pages the repository reads is not read to its end, and is refused rather than cut short.
    /// </summary>
    async Task<IReadOnlyList<JsonElement>> ResourcesOf(string path, ListQuery query)
    {
        return await Client.GetAll(path, new ListQuery(query.Filter, query.Sort, PAGE_SIZE), MAX_PAGES);
    }
}

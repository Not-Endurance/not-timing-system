using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Not.Application.CRUD.Ports;
using Not.Application.HTTP;
using Not.Domain.Abstractions;
using Not.Krud.Abstractions;
using Not.Notify;

namespace Not.Storage.REST;

/// <summary>
/// A repository whose resource moved to the Api and is read and written as the JSON:API documents of ADR-0008: the other
/// variant of <see cref="ApiRepository{T, TModel}"/>, which a family opts into in the ticket that ports it. A resource
/// object holds the model as its attributes, in camelCase, and the id as its own; a list is read a page at a time until
/// the last; a filter is the OData expression the server applies, and one that cannot be written is applied here to what
/// is read, as it is for the other variant. What a request answers is not swallowed: an error is read with its code, a
/// person is told what it says, and a row that is not there is not an error.
/// </summary>
public abstract class JsonApiRepository<T, TModel> : IRepository<T>
    where T : class, IEntity
    where TModel : class, IKrudModel<T>, new()
{
    const int PAGE_SIZE = 500;
    const int MAX_PAGES = 1000;
    static readonly PropertyInfo ID = typeof(TModel).GetProperty("Id")!;

    readonly string _collection;
    readonly HashSet<string> _notSent;
    readonly IRepositoryScopeFactory<T>? _scopeFactory;

    /// <param name="collection">The type of the resource and the path of its collection, such as <c>clubs</c>.</param>
    /// <param name="notSent">
    /// The members of the model, in camelCase, that the Api owns or keeps to itself, so that they are not sent back: the
    /// id and the Tenant never are.
    /// </param>
    protected JsonApiRepository(string collection, JsonApiClient client, params string[] notSent)
        : this(collection, client, null, notSent) { }

    /// <param name="collection">The type of the resource and the path of its collection, such as <c>clubs</c>.</param>
    /// <param name="scopeFactory">
    /// What every list is limited to, such as the Event whose rows these are, and which the Api asks every list of the
    /// resource to name. None reads whatever the filter of a call says.
    /// </param>
    /// <param name="notSent">
    /// The members of the model, in camelCase, that the Api owns or keeps to itself, so that they are not sent back: the
    /// id and the Tenant never are.
    /// </param>
    protected JsonApiRepository(
        string collection,
        JsonApiClient client,
        IRepositoryScopeFactory<T>? scopeFactory,
        params string[] notSent
    )
    {
        _collection = collection;
        Client = client;
        _scopeFactory = scopeFactory;
        _notSent = ["id", "tenantId", .. notSent];
    }

    static INotifier? Notifier => NotificationHelper.Current;

    protected JsonApiClient Client { get; }
    protected string Collection => _collection;

    /// <summary>The members a new resource is made with; none means every member there is.</summary>
    protected virtual IReadOnlyCollection<string>? CreateMembers => null;

    /// <summary>
    /// The members that are written when the resource is made and a change leaves as they are, such as the Event of a row:
    /// the Api refuses a change that names one.
    /// </summary>
    protected virtual IReadOnlyCollection<string> NotChanged => [];

    /// <summary>
    /// What the Api answered to the last thing this repository did when it did not take it, with its code: a form that
    /// asked can branch on it, and a person has been told what it says. None when the last request was taken.
    /// </summary>
    public JsonApiError? LastError { get; private set; }

    /// <summary>What happened to a request that was answered with an error, to be told to a person.</summary>
    protected virtual void OnError(JsonApiError error)
    {
        Notifier?.Warn(error.Message);
    }

    protected virtual async Task<bool> CreateCore(T item)
    {
        var members = CreateMembers;
        var response = await Client.Send(HttpMethod.Post, _collection, Document(MapModel(item), members));
        return response.IsSuccess ? Done() : Failed(response);
    }

    protected virtual async Task<bool> UpdateCore(T item)
    {
        var model = MapModel(item);
        var response = await Client.Send(
            HttpMethod.Patch,
            $"{_collection}/{item.Id}",
            Document(model, null, NotChanged, ChangeMeta(model))
        );
        return response.IsSuccess ? Done() : Failed(response);
    }

    /// <summary>
    /// What the document of a change says of the resource besides its attributes, in its <c>meta</c>: the version of a
    /// resource that counts its writes, which the change is based on. None by default.
    /// </summary>
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
    /// Every resource of a named collection of the resource, read a page at a time, in the order asked for (<c>-endDay</c>
    /// is the last day first): <c>events/live</c> is the view <c>live</c> of <c>events</c>.
    /// </summary>
    protected Task<IEnumerable<T>> ReadView(string view, string? sort = null)
    {
        return ReadPages(null, $"{_collection}/{view}", sort);
    }

    /// <summary>The resource of a document the Api answered with, as the entity it is.</summary>
    protected T? EntityOf(JsonElement? resource)
    {
        if (resource is not { ValueKind: JsonValueKind.Object } data)
        {
            return null;
        }

        var model = data.TryGetProperty("attributes", out var attributes)
            ? JsonSerializer.Deserialize<TModel>(attributes.GetRawText(), JsonApiClient.Options)
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

    protected bool Done()
    {
        return true;
    }

    /// <param name="response">The answer that was not taken.</param>
    /// <param name="tell">Whether a person is told what it says: not when the caller tells them itself, by what it throws.</param>
    protected bool Failed(JsonApiResponse response, bool tell = true)
    {
        LastError = response.Error ?? new JsonApiError((int)response.Status, null, null, null);
        if (tell)
        {
            OnError(LastError);
        }

        return false;
    }

    /// <summary>
    /// The document of a write: the resource with the id of the model, the members it is sent with, and what it says of
    /// itself besides them. Without a list of members it is sent with every one but the ones the Api keeps and the ones
    /// that are left out.
    /// </summary>
    protected JsonElement Document(
        TModel model,
        IReadOnlyCollection<string>? members,
        IReadOnlyCollection<string>? leftOut = null,
        JsonObject? meta = null
    )
    {
        var attributes = new JsonObject();
        var serialized = JsonSerializer.SerializeToElement(model, JsonApiClient.WriteOptions);
        foreach (var member in serialized.EnumerateObject())
        {
            var sent =
                members == null
                    ? !_notSent.Contains(member.Name) && leftOut?.Contains(member.Name) != true
                    : members.Contains(member.Name);
            if (sent)
            {
                attributes[member.Name] = JsonNode.Parse(member.Value.GetRawText());
            }
        }

        var data = new JsonObject { ["type"] = _collection };
        var id = (Guid)ID.GetValue(model)!;
        if (id != Guid.Empty)
        {
            data["id"] = id.ToString();
        }

        data["attributes"] = attributes;
        if (meta != null)
        {
            data["meta"] = meta;
        }

        return JsonSerializer.SerializeToElement(new JsonObject { ["data"] = data });
    }

    public async Task Create(T item)
    {
        await Guarded(() => CreateCore(item));
    }

    public async Task Update(T item)
    {
        await Guarded(() => UpdateCore(item));
    }

    public async Task Delete(Guid id)
    {
        await Guarded(async () =>
        {
            var response = await Client.Send(HttpMethod.Delete, $"{_collection}/{id}");

            // A row that is not there is what the caller wanted it to be.
            return response.IsSuccess || response.IsNotFound ? Done() : Failed(response);
        });
    }

    public async Task Delete(T item)
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

    public async Task<T?> Read(Guid id)
    {
        T? read = null;
        await Guarded(async () =>
        {
            var response = await Client.Send(HttpMethod.Get, $"{_collection}/{id}");
            if (response.IsNotFound)
            {
                return Done();
            }

            if (!response.IsSuccess)
            {
                return Failed(response);
            }

            read = EntityOf(response.Document?.GetProperty("data"));
            return Done();
        });
        return read;
    }

    public virtual async Task<IEnumerable<T>> ReadMany()
    {
        return await ReadWithin(null);
    }

    public virtual async Task<IEnumerable<T>> ReadMany(Expression<Func<T, bool>> filter)
    {
        return await ReadWithin(filter);
    }

    /// <summary>
    /// What satisfies the filter within the scope of the repository, the scope first, as the Api asks of a list of what an
    /// Event keeps. What cannot be written for it is read within the scope and filtered here.
    /// </summary>
    async Task<IEnumerable<T>> ReadWithin(Expression<Func<T, bool>>? filter)
    {
        var scope = _scopeFactory?.Create().Filter;
        if (ODataApiFilterAdapter.TryParseFilters(Filters(scope, filter), out var parameters, camelCase: true))
        {
            return await ReadPages(parameters.GetValueOrDefault("$filter"));
        }

        if (filter == null)
        {
            return await ReadPages(null);
        }

        // The expression cannot be written for the server, so what there is is read and filtered here.
        var predicate = filter.Compile();
        var within = ODataApiFilterAdapter.TryParseFilters(Filters(scope, null), out var scoped, camelCase: true)
            ? await ReadPages(scoped.GetValueOrDefault("$filter"))
            : await ReadPages(null);
        return within.Where(predicate);
    }

    static Expression<Func<T, bool>>[] Filters(Expression<Func<T, bool>>? scope, Expression<Func<T, bool>>? filter)
    {
        return [.. new[] { scope, filter }.OfType<Expression<Func<T, bool>>>()];
    }

    async Task<IEnumerable<T>> ReadPages(string? filter, string? path = null, string? sort = null)
    {
        var items = new List<T>();
        var complete = false;
        await Guarded(async () =>
        {
            for (var number = 1; number <= MAX_PAGES; number++)
            {
                var query = $"page[size]={PAGE_SIZE}&page[number]={number}";
                if (sort != null)
                {
                    query = $"sort={Uri.EscapeDataString(sort)}&{query}";
                }

                if (filter != null)
                {
                    query = $"filter={Uri.EscapeDataString(filter)}&{query}";
                }

                var response = await Client.Send(HttpMethod.Get, $"{path ?? _collection}?{query}");
                if (!response.IsSuccess)
                {
                    return Failed(response);
                }

                var document = response.Document!.Value;
                items.AddRange(document.GetProperty("data").EnumerateArray().Select(x => EntityOf(x)).OfType<T>());
                if (!HasNext(document))
                {
                    break;
                }
            }

            complete = true;
            return Done();
        });

        // A list that could not be read to its end is not a list: what a caller decides by it, or removes by it, is of all.
        return complete ? items : [];
    }

    static bool HasNext(JsonElement document)
    {
        return document.TryGetProperty("links", out var links)
            && links.ValueKind == JsonValueKind.Object
            && links.TryGetProperty("next", out var next)
            && next.ValueKind == JsonValueKind.String;
    }

    /// <summary>Runs a request and tells a person when it did not arrive, as the other variant does.</summary>
    async Task Guarded(Func<Task<bool>> request)
    {
        LastError = null;
        try
        {
            await request();
        }
        catch (Exception ex)
        {
            if (
                ex is HttpRequestException httpRequestException
                && httpRequestException.HttpRequestError == HttpRequestError.ConnectionError
            )
            {
#if DEBUG
                Notifier?.Error(ex);
#else
                Notifier?.Warn(Not.Localization.NStrings.Cannot_connect_to_server_string);
#endif
            }
            else
            {
                Notifier?.Error(ex);
            }
        }
    }
}

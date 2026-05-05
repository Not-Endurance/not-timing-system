using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using MongoDB.Driver;
using Not.Application.CRUD.Ports;
using Not.Application.HTTP;
using Not.Domain.Abstractions;
using Not.Exceptions;
using Not.Krud.Abstractions;
using Not.Notify;
using Not.Structures;

namespace Not.Storage.REST;

public abstract class ApiRepository<T, TModel> : IRepository<T>
    where T : class, IEntity
    where TModel : class, IKrudModel<T>, new()
{
    readonly string _endpoint;
    readonly IRepositoryScopeFactory<T>? _scopeFactory;

    protected ApiRepository(string endpoint, NHttpClient client, IRepositoryScopeFactory<T>? scopeFactory = null)
    {
        _endpoint = endpoint;
        Client = client;
        _scopeFactory = scopeFactory;
    }

    static INotifier? Notifier => NotificationHelper.Current;

    protected NHttpClient Client { get; }
    protected string Endpoint => _endpoint;

    protected virtual string BuildEndpoint(object urn, Expression<Func<T, bool>>? filter = null)
    {
        GuardHelper.ThrowIfNullOrWhiteSpace(urn);
        return BuildEndpointCore($"{Endpoint}/{urn}", filter);
    }

    protected virtual string BuildEndpoint(object urn1, object urn2, Expression<Func<T, bool>>? filter = null)
    {
        GuardHelper.ThrowIfNullOrWhiteSpace(urn1);
        GuardHelper.ThrowIfNullOrWhiteSpace(urn2);
        return BuildEndpointCore($"{Endpoint}/{urn1}/{urn2}", filter);
    }

    protected virtual string BuildEndpoint(object urn1, object urn2, object urn3, Expression<Func<T, bool>>? filter = null)
    {
        GuardHelper.ThrowIfNullOrWhiteSpace(urn1);
        GuardHelper.ThrowIfNullOrWhiteSpace(urn2);
        GuardHelper.ThrowIfNullOrWhiteSpace(urn3);
        return BuildEndpointCore($"{Endpoint}/{urn1}/{urn2}/{urn3}", filter);
    }

    protected virtual string BuildEndpoint(Expression<Func<T, bool>>? filter = null)
    {
        return BuildEndpointCore(Endpoint, filter);
    }

    protected virtual Task<Result<TModel>> CreateCore(T item)
    {
        var model = MapModel(item);
        return Client.Post(BuildEndpoint(), model);
    }

    protected virtual Task<Result<TModel>> UpdateCore(T item)
    {
        var model = MapModel(item);
        return Client.Patch(BuildEndpoint(), model);
    }

    protected virtual TModel MapModel(T item)
    {
        var model = new TModel();
        model.MapFrom(item);
        return model;
    }

    [return: NotNullIfNotNull(nameof(model))]
    protected virtual T? MapEntity(TModel? model)
    {
        return model?.MapToEntity();
    }

    protected virtual async Task<TItem?> HandleRequest<TItem>(Task<Result<TItem>> request)
        where TItem : class
    {
        try
        {
            var result = await request;
            if (!result.IsSuccess)
            {
                Notifier?.Warn(string.Join(Environment.NewLine, result.Errors));
                return null;
            }
            return result.Data;
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
            return null;
        }
    }

    public async Task Create(T item)
    {
        await HandleRequest(CreateCore(item));
    }

    public async Task Delete(int id)
    {
        var url = BuildEndpoint(id);
        await HandleRequest(Client.Delete(url));
    }

    public async Task Delete(T item)
    {
        await Delete(item.Id);
    }

    public virtual async Task DeleteMany(Expression<Func<T, bool>> filter)
    {
        var items = await ReadMany(filter);
        await DeleteMany(items);
    }

    public virtual async Task DeleteMany(IEnumerable<T> items)
    {
        var models = items.Select(MapModel).ToArray();
        if (models.Length == 0)
        {
            return;
        }

        await HandleRequest(Client.Delete(BuildEndpoint(), models));
    }

    public virtual async Task<T?> Read(Expression<Func<T, bool>> filter)
    {
        return (await ReadMany(filter)).FirstOrDefault();
    }

    public async Task<T?> Read(int id)
    {
        var url = BuildEndpoint(id);
        var model = await HandleRequest(Client.Get<TModel>(url));
        return MapEntity(model);
    }

    public virtual async Task<IEnumerable<T>> ReadMany()
    {
        var models = await HandleRequest(Client.Get<IEnumerable<TModel>>(BuildEndpoint())) ?? [];
        return models.Select(x => MapEntity(x)!);
    }

    public virtual async Task<IEnumerable<T>> ReadMany(Expression<Func<T, bool>> filter)
    {
        if (ODataApiFilterAdapter.TryParseFilters(GetFilters(filter), out _))
        {
            var endpoint = BuildEndpoint(filter);
            var models = await HandleRequest(Client.Get<IEnumerable<TModel>>(endpoint)) ?? [];
            return models.Select(x => MapEntity(x)!);
        }

        // TODO: Warning logc with trace measurements for the fallback
        var predicate = filter.Compile();
        var results = await ReadMany();
        return results.Where(predicate);
    }

    public async Task Update(T item)
    {
        await HandleRequest(UpdateCore(item));
    }

    string  BuildEndpointCore(string endpoint, Expression<Func<T, bool>>? filter = null)
    {
        var queryParams = ODataApiFilterAdapter.ParseFilters(GetFilters(filter));
        return HttpHelper.AddQueryString(endpoint, queryParams);
    }

    IEnumerable<Expression<Func<T, bool>>> GetFilters(Expression<Func<T, bool>>? filter = null)
    {
        var filters = new List<Expression<Func<T, bool>>>();
        if (_scopeFactory != null)
        {
            filters.Add(_scopeFactory.Create().Filter);
        }
        if (filter != null)
        {
            filters.Add(filter);
        }

        return filters;
    }
}

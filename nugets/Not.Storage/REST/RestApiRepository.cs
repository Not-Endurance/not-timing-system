using System.Linq.Expressions;
using Not.Application.CRUD.Ports;
using Not.Application.HTTP;
using Not.Domain.Abstractions;
using Not.Krud.Abstractions;
using Not.Notify;
using Not.Structures;

namespace Not.Storage.REST;

public abstract class RestApiRepository<T, TModel> : IRepository<T>
    where T : class, IEntity
    where TModel : class, IKrudModel<T>, new()
{
    readonly string _endpoint;

    protected RestApiRepository(string endpoint, NHttpClient client)
    {
        _endpoint = endpoint;
        Client = client;
    }

    static INotifier? Notifier => NotificationHelper.Current;

    protected NHttpClient Client { get; }
    protected string Endpoint => _endpoint;

    protected virtual string ResolveEndpoint()
    {
        return _endpoint;
    }

    protected virtual string BuildUrl(object id)
    {
        return $"{ResolveEndpoint()}/{id}";
    }

    protected virtual Task<Result<TModel>> CreateCore(T item)
    {
        var model = MapModel(item);
        return Client.Post(ResolveEndpoint(), model);
    }

    protected virtual Task<Result<TModel>> UpdateCore(T item)
    {
        var model = MapModel(item);
        return Client.Patch(ResolveEndpoint(), model);
    }

    protected virtual TModel MapModel(T item)
    {
        var model = new TModel();
        model.MapFrom(item);
        return model;
    }

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
                Notifier?.Warn(ex.Message);
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
        var url = BuildUrl(id);
        await HandleRequest(Client.Delete(url));
    }

    public async Task Delete(T item)
    {
        await Delete(item.Id);
    }

    public virtual Task Delete(Expression<Func<T, bool>> filter)
    {
        return DeleteMany(filter);
    }

    public virtual Task Delete(IEnumerable<T> items)
    {
        return DeleteMany(items);
    }

    public virtual async Task<T?> Read(Expression<Func<T, bool>> filter)
    {
        return (await ReadMany(filter)).FirstOrDefault();
    }

    public async Task<T?> Read(int id)
    {
        var url = BuildUrl(id);
        var model = await HandleRequest(Client.Get<TModel>(url));
        return MapEntity(model);
    }

    public virtual async Task<IEnumerable<T>> ReadMany()
    {
        var models = await HandleRequest(Client.Get<IEnumerable<TModel>>(ResolveEndpoint())) ?? [];
        return models.Select(x => MapEntity(x)!);
    }

    public virtual async Task<IEnumerable<T>> ReadMany(Expression<Func<T, bool>> filter)
    {
        var predicate = filter.Compile();
        return (await ReadMany()).Where(predicate);
    }

    public async Task Update(T item)
    {
        await HandleRequest(UpdateCore(item));
    }

    async Task DeleteMany(Expression<Func<T, bool>> filter)
    {
        var items = await ReadMany(filter);
        await DeleteMany(items);
    }

    async Task DeleteMany(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            await Delete(item);
        }
    }
}

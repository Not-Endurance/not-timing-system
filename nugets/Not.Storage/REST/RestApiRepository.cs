using System.Linq.Expressions;
using Not.Application.CRUD.Ports;
using Not.Application.HTTP;
using Not.Domain.Abstractions;
using Not.Krud.Abstractions;
using Not.Notify;

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

    protected virtual void HandleException(Exception ex)
    {
        if (
            ex is HttpRequestException httpRequestException
            && httpRequestException.HttpRequestError == HttpRequestError.ConnectionError
        )
        {
#if DEBUG
            Notifier?.Warn(ex.Message);
#else
            Notifier?.Warn(
                NStrings.Could_not_connect_to_Nexus_Some_operations_will_not_be_available_Please_check_your_internet_connection
            );
#endif
        }
        else
        {
            Notifier?.Error(ex);
        }
    }

    protected virtual Task InternalCreate(T item)
    {
        var model = MapModel(item);
        return Client.Post(ResolveEndpoint(), model);
    }

    protected virtual Task InternalUpdate(T item)
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

    public async Task Create(T item)
    {
        try
        {
            await InternalCreate(item);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    public async Task Delete(int id)
    {
        try
        {
            var url = BuildUrl(id);
            await Client.Delete(url);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
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
        try
        {
            var url = BuildUrl(id);
            return MapEntity(await Client.GetJson<TModel>(url));
        }
        catch (Exception ex)
        {
            HandleException(ex);
            return null;
        }
    }

    public virtual async Task<IEnumerable<T>> ReadMany()
    {
        try
        {
            var models = await Client.GetJson<IEnumerable<TModel>>(ResolveEndpoint()) ?? [];
            return models.Select(x => MapEntity(x)!);
        }
        catch (Exception ex)
        {
            HandleException(ex);
            return [];
        }
    }

    public virtual async Task<IEnumerable<T>> ReadMany(Expression<Func<T, bool>> filter)
    {
        var predicate = filter.Compile();
        return (await ReadMany()).Where(predicate);
    }

    public async Task Update(T item)
    {
        try
        {
            await InternalUpdate(item);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
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

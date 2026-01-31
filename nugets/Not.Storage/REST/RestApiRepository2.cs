using System.Linq.Expressions;
using Not.Application.CRUD.Ports;
using Not.Application.HTTP;
using Not.Application.Krud.Abstractions;
using Not.Domain.Abstractions;
using Not.Notify;

namespace Not.Storage.REST;

public abstract class RestApiRepository2<T, TModel> : IRepository<T>
    where T : class, IEntity
    where TModel : class, IKrudModel<T>, new()
{
    readonly string _endpoint;

    protected RestApiRepository2(string endpoint, NHttpClient client)
    {
        _endpoint = endpoint;
        Client = client;
    }

    protected NHttpClient Client { get; }

    protected string BuildUrl(object id)
    {
        return $"{_endpoint}/{id}";
    }

    protected void HandleException(Exception ex)
    {
        if (
            ex is HttpRequestException httpRequestException
            && httpRequestException.HttpRequestError == HttpRequestError.ConnectionError
        )
        {
#if DEBUG
            NotifyHelper.Warn(ex.Message);
#else
            NotifyHelper.Warn(
                Localization
                    .NStrings
                    .Could_not_connect_to_Nexus_Some_operations_will_not_be_available_Please_check_your_internet_connection
            );
#endif
        }
        else
        {
            NotifyHelper.Error(ex);
        }
    }

    public async Task Create(T item)
    {
        try
        {
            var model = MapModel(item);
            await Client.Post(_endpoint, model);
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

    public Task Delete(Expression<Func<T, bool>> filter)
    {
        throw new NotImplementedException();
    }

    public Task Delete(IEnumerable<T> items)
    {
        throw new NotImplementedException();
    }

    public Task<T?> Read(Expression<Func<T, bool>> filter)
    {
        throw new NotImplementedException();
    }

    public async Task<T?> Read(int id)
    {
        try
        {
            var url = BuildUrl(id);
            var model = await Client.GetJson<TModel>(url);
            return MapEntity(model);
        }
        catch (Exception ex)
        {
            HandleException(ex);
            return null;
        }
    }

    public async Task<IEnumerable<T>> ReadMany()
    {
        try
        {
            var models = await Client.GetJson<IEnumerable<TModel>>(_endpoint) ?? [];
            return models.Select(x => MapEntity(x)!);
        }
        catch (Exception ex)
        {
            HandleException(ex);
            return [];
        }
    }

    public Task<IEnumerable<T>> ReadMany(Expression<Func<T, bool>> filter)
    {
        throw new NotImplementedException();
    }

    public async Task SafeDelete(int id)
    {
        try
        {
            var url = $"{BuildUrl(id)}/safe";
            await Client.Delete(url);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    public async Task Update(T item)
    {
        try
        {
            var model = MapModel(item);
            await Client.Patch(_endpoint, model);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    TModel MapModel(T item)
    {
        var model = new TModel();
        model.MapFrom(item);
        return model;
    }

    T? MapEntity(TModel? model)
    {
        return model?.MapToEntity();
    }
}

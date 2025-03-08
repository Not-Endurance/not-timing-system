using System.Linq.Expressions;
using Not.Application.CRUD.Ports;
using Not.Domain;
using Not.Notify;
using Not.Serialization;
using Not.Structures;

namespace Not.Application.HTTP;

public class HttpRepository<T> : IRepository<T>, ISafeDelete<T>
    where T : class, IAggregateRoot, IIdentifiable
{
    const string ERROR_TEMPLATE = "Could not connect to Nexus. Some operations will not be available. Please check your internet connection";

    readonly string _endpoint;

    public HttpRepository(string endpoint, NHttpClient client)
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
        var message = ex is HttpRequestException httpRequestException && httpRequestException.HttpRequestError == HttpRequestError.ConnectionError
            ? ERROR_TEMPLATE
            : ex.Message;
        NotifyHelper.Warn(message);
    }

    public async Task Create(T entity)
    {
        try
        {
            await Client.Post(_endpoint, entity);
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

    public async Task Delete(T entity)
    {
        await Delete(entity.Id);
    }

    public Task Delete(Expression<Func<T, bool>> filter)
    {
        throw new NotImplementedException();
    }

    public Task Delete(IEnumerable<T> entities)
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
            var content = await Client.Get(url);
            return content?.FromJson<T>();
        }
        catch (Exception ex)
        {
            HandleException(ex);
            return null;
        }
    }

    public async Task<IEnumerable<T>> ReadAll()
    {
        try
        {
            var content = await Client.Get(_endpoint);
            return content?.FromJson<IEnumerable<T>>() ?? [];
        }
        catch (Exception ex)
        {
            HandleException(ex);
            return [];
        }
    }

    public Task<IEnumerable<T>> ReadAll(Expression<Func<T, bool>> filter)
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

    public async Task Update(T entity)
    {
        try
        {
            await Client.Patch(_endpoint, entity);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }
}

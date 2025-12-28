using System.Linq.Expressions;
using Not.Application.CRUD.Ports;
using Not.Domain.Aggregates;
using Not.Notify;
using Not.Serialization.JSON;
using Not.Structures;

namespace Not.Application.HTTP;

public abstract class HttpRepository<T> : IRepository<T>, ISafeDelete<T>
    where T : class, IAggregateRoot, IIdentifiable
{
    readonly string _endpoint;

    protected HttpRepository(string endpoint, NHttpClient client)
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
            NotifyHelper.Warn(
                Could_not_connect_to_Nexus_Some_operations_will_not_be_available_Please_check_your_internet_connection
            );
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
            await Client.Post(_endpoint, item);
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
            return await Client.GetJson<T>(url);
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
            return await Client.GetJson<IEnumerable<T>>(_endpoint) ?? [];
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

    public async Task Update(T items)
    {
        try
        {
            await Client.Patch(_endpoint, items);
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }
}

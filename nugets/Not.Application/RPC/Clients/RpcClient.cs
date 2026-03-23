using Microsoft.AspNetCore.SignalR.Client;
using Not.Application.RPC.SignalR;
using Not.Reflection;
using Not.Startup;

namespace Not.Application.RPC.Clients;

public abstract class RpcClient : IRpcClient
{
    readonly SignalRSocket _socket;
    bool _isInitialized;

    protected RpcClient(IRpcSocket socket)
    {
        if (socket is not SignalRSocket signalRSocket)
        {
            throw new ApplicationException($"Unsupported socket '{socket?.GetTypeName()}'");
        }
        _socket = signalRSocket;
    }

    protected abstract void RegisterProcedures();

    protected SignalRSocket Socket => _socket;

    internal void EnsureInitialized()
    {
        if (_isInitialized)
        {
            return;
        }

        RegisterProcedures();
        _isInitialized = true;
    }

    public virtual async Task Connect()
    {
        EnsureInitialized();
        if (_socket.IsConnected)
        {
            return;
        }
        await _socket.Connect();
    }

    public virtual async Task Connect(string groupId)
    {
        EnsureInitialized();
        if (_socket.IsConnected)
        {
            return;
        }
        await _socket.Connect(groupId);
    }

    public async Task Disconnect()
    {
        if (!_socket.IsConnected)
        {
            return;
        }
        await _socket.Disconnect();
    }

    public void RunAtStartup()
    {
        EnsureInitialized();
    }

    public void RegisterInputProcedure(string name, Func<Task> action)
    {
        _socket.Procedures.Add(connection =>
        {
            connection.On(
                name,
                () =>
                {
                    try
                    {
                        action();
                    }
                    catch (Exception exception)
                    {
                        _socket.RaiseError(exception, name);
                    }
                }
            );
        });
    }

    public void RegisterOutputCollectionProcedure<T>(string name, Func<Task<IEnumerable<T>>> action)
        where T : class
    {
        _socket.Procedures.Add(connection =>
        {
            connection.On(
                name,
                (Func<Task<IEnumerable<T>>>)(async () =>
                {
                    try
                    {
                        return await action();
                    }
                    catch (Exception exception)
                    {
                        _socket.RaiseError(exception, name);
                        return [];
                    }
                })
            );
        });
    }

    public void RegisterOutputProcedure<T>(string name, Func<Task<T?>> action)
        where T : struct
    {
        _socket.Procedures.Add(connection =>
        {
            connection.On(
                name,
                (Func<Task<T?>>)(async () =>
                {
                    try
                    {
                        return await action();
                    }
                    catch (Exception exception)
                    {
                        _socket.RaiseError(exception, name);
                        return null;
                    }
                })
            );
        });
    }

    public void RegisterOutputProcedure<T>(string name, Func<Task<T?>> action)
        where T : class
    {
        _socket.Procedures.Add(connection =>
        {
            connection.On(
                name,
                (Func<Task<T?>>)(async () =>
                {
                    try
                    {
                        return await action();
                    }
                    catch (Exception exception)
                    {
                        _socket.RaiseError(exception, name);
                        return null;
                    }
                })
            );
        });
    }

    public void RegisterInputProcedure<T>(string name, Func<T, Task> action)
    {
        _socket.Procedures.Add(connection =>
        {
            connection.On<T>(
                name,
                a =>
                {
                    try
                    {
                        action(a);
                    }
                    catch (Exception exception)
                    {
                        _socket.RaiseError(exception, name, a);
                    }
                }
            );
        });
    }

    public void RegisterInputProcedure<T1, T2>(string name, Func<T1, T2, Task> action)
    {
        _socket.Procedures.Add(connection =>
        {
            connection.On<T1, T2>(
                name,
                async (a, b) =>
                {
                    try
                    {
                        await action(a, b);
                    }
                    catch (Exception exception)
                    {
                        _socket.RaiseError(exception, name, a, b);
                    }
                }
            );
        });
    }

    public void RegisterInputProcedure<T1, T2, T3>(string name, Func<T1, T2, T3, Task> action)
    {
        _socket.Procedures.Add(connection =>
        {
            connection.On<T1, T2, T3>(
                name,
                (a, b, c) =>
                {
                    try
                    {
                        action(a, b, c);
                    }
                    catch (Exception exception)
                    {
                        _socket.RaiseError(exception, name, a, b, c);
                    }
                }
            );
        });
    }

}

public interface IRpcClient : IStartupInitializer
{
    Task Connect();
    Task Connect(string groupId);
    Task Disconnect();
}

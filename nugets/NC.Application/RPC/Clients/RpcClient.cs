using Microsoft.AspNetCore.SignalR.Client;
using NC.Application.Identity;
using NC.Application.RPC.Changes;
using NC.Application.RPC.SignalR;
using NC.Reflection;
using NC.Startup;

namespace NC.Application.RPC.Clients;

public abstract class RpcClient : IRpcClient
{
    readonly IClientIdentity _identity;
    readonly SignalRSocket _socket;
    bool _isInitialized;

    protected RpcClient(IRpcSocket socket, IClientIdentity identity)
    {
        if (socket is not SignalRSocket signalRSocket)
        {
            throw new ApplicationException($"Unsupported socket '{socket?.GetTypeName()}'");
        }
        _socket = signalRSocket;
        _identity = identity;
    }

    protected abstract void RegisterProcedures();

    protected SignalRSocket Socket => _socket;

    /// <summary>
    /// Registers a change notice. The action receives whether the change came from another client.
    /// A client's own writes are still delivered, so its state refreshes, but they are not marked as remote.
    /// </summary>
    protected void RegisterChangeProcedure<T>(string name, Func<T, bool, Task> action)
        where T : ChangeNotice
    {
        RegisterInputProcedure<T>(
            name,
            async notice =>
            {
                try
                {
                    await action(notice, !notice.IsFrom(_identity.Id));
                }
                catch (Exception exception)
                {
                    _socket.RaiseError(exception, name, notice);
                }
            }
        );
    }

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
                (Func<Task<IEnumerable<T>>>)(
                    async () =>
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
                    }
                )
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
                (Func<Task<T?>>)(
                    async () =>
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
                    }
                )
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
                (Func<Task<T?>>)(
                    async () =>
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
                    }
                )
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

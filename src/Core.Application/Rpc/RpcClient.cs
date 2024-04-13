using Core.Application.Rpc.Procedures;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Threading.Tasks;

namespace Core.Application.Rpc;

public abstract class RpcClient
{
    private readonly SignalRSocket _socket;

    protected RpcClient(SignalRSocket socket)
    {
        _socket = socket;
    }

    public void RegisterClientProcedure(string name, Func<Task> action)
    {
        _socket.Procedures.Add(connection =>
        {
            connection.On(name, () =>
            {
                try
                {
                    action();
                }
                catch (Exception exception)
                {
                    _socket.RaiseError(exception, name);
                }
            });
        });
    }

    public void RegisterClientProcedure<T>(string name, Func<T, Task> action)
    {
        _socket.Procedures.Add(connection =>
        {
            connection.On<T>(name, a =>
            {
                try
                {
                    action(a);
                }
                catch (Exception exception)
                {
                    _socket.RaiseError(exception, name, a);
                }
            });
        });
    }

    public void RegisterClientProcedure<T1, T2>(string name, Func<T1, T2, Task> action)
    {
        _socket.Procedures.Add(connection =>
        {
            connection.On<T1, T2>(name, (a, b) =>
            {
                try
                {
                    action(a, b);
                }
                catch (Exception exception)
                {
                    _socket.RaiseError(exception, name, a, b);
                }
            });
        });
    }

    public void RegisterClientProcedure<T1, T2, T3>(string name, Func<T1, T2, T3, Task> action)
    {
        _socket.Procedures.Add(connection =>
        {
            connection.On<T1, T2, T3>(name, (a, b, c) =>
            {
                try
                {
                    action(a, b, c);
                }
                catch (Exception exception)
                {
                    _socket.RaiseError(exception, name, a, b, c);
                }
            });
        });
    }

    public async Task<RpcInvokeResult> InvokeHubProcedure<T>(string name, T parameter)
    {
        try
        {
            await _socket.Connection.InvokeAsync(name, parameter);
            return RpcInvokeResult.Success;
        }
        catch (Exception exception)
        {
            _socket.RaiseError(exception, name, parameter);
            return RpcInvokeResult.Error;
        }
    }

    public async Task<RpcInvokeResult> InvokeHubProcedure<T1, T2>(string name, T1 parameter1, T2 parameter2)
    {
        try
        {
            await _socket.Connection.InvokeAsync(name, parameter1, parameter2);
            return RpcInvokeResult.Success;
        }
        catch (Exception exception)
        {
            _socket.RaiseError(exception, name, parameter1, parameter2);
            return RpcInvokeResult.Error;
        }
    }

    public async Task<RpcInvokeResult<T>> InvokeHubProcedure<T>(string name)
    {
        try
        {
            var result = await _socket.Connection.InvokeAsync<T>(name);
            return RpcInvokeResult<T>.Success(result);
        }
        catch (Exception exception)
        {
            _socket.RaiseError(exception, name);
            return RpcInvokeResult<T>.Error;
        }
    }
}
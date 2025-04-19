using Microsoft.AspNetCore.SignalR.Client;
using Not.Application.RPC.Clients;
using Not.Application.RPC.SignalR;

namespace Not.Application.RPC;

public static class RpcSocketExtensions
{
    public static async Task<RpcInvokeResult> InvokeInputProcedure<T>(this IRpcSocket socket, string name, T parameter)
    {
        try
        {
            await EnsureConnected(socket);
            await socket.Connection!.InvokeAsync(name, parameter);
            return RpcInvokeResult.Success;
        }
        catch (Exception exception)
        {
            socket.RaiseError(exception, name, parameter);
            return RpcInvokeResult.Error;
        }
    }

    public static async Task<RpcInvokeResult> InvokeInputProcedure<T1, T2>(this IRpcSocket socket, string name, T1 parameter1, T2 parameter2)
    {
        try
        {
            await EnsureConnected(socket);
            await socket.Connection!.InvokeAsync(name, parameter1, parameter2);
            return RpcInvokeResult.Success;
        }
        catch (Exception exception)
        {
            socket.RaiseError(exception, name, parameter1, parameter2);
            return RpcInvokeResult.Error;
        }
    }

    public static async Task<RpcInvokeResult<T>> InvokeOutputProcedure<T>(this IRpcSocket socket, string name)
    {
        try
        {
            await EnsureConnected(socket);
            var result = await socket.Connection!.InvokeAsync<T>(name);
            return RpcInvokeResult<T>.Success(result);
        }
        catch (Exception exception)
        {
            socket.RaiseError(exception, name);
            return RpcInvokeResult<T>.Error;
        }
    }

    static async Task EnsureConnected(this IRpcSocket socket)
    {
        if (!socket.IsConnected)
        {
            return;
        }

        await socket.Connect();
    }
}

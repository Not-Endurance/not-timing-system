using Core.Application.Rpc.Procedures;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Application.Rpc;

public class RpcClient : IRpcClient, IAsyncDisposable
{
	private const int AUTOMATIC_RECONNECT_ATTEMPTS = 3;
	private int _reconnectAttempts;
    public event EventHandler<RpcConnectionStatus>? ServerConnectionChanged;
	public event EventHandler<string>? ServerConnectionInfo;
	public bool IsConnected => this.Connection?.State == HubConnectionState.Connected;

    private readonly RpcContext context;

    // Necessary because this.Connection instance is not intialized 
    // when procedures are reigstered in the child constructor
    private List<Action<HubConnection>> procedureRegistrations = new();
	private CancellationTokenSource reconnectTokenSource;

    public RpcClient(RpcContext context)
    {
        this.context = context;
        this.reconnectTokenSource = new CancellationTokenSource();
    }
    
    protected HubConnection? Connection { get; private set; }

    public virtual async Task Connect(string host)
    {
		_reconnectAttempts = 0;
        await InternalConnect(host);
	}

	private async Task InternalConnect(string host)
	{
        if (this.reconnectTokenSource.IsCancellationRequested)
        {
            this.reconnectTokenSource = new CancellationTokenSource();
        }
        if (this.Connection == null)
        {
            this.ConfigureConnection(host);
        }
        RaiseConnecting();
        try
        {
            await this.Connection!.StartAsync();
            this.RaiseConnected();
        }
        catch (Exception ex)
        {
            this.RaiseError(ex, this.context.Url);
            RaiseDisconnected(ex);
            if (_reconnectAttempts < AUTOMATIC_RECONNECT_ATTEMPTS)
            {
                _reconnectAttempts++;
                await InternalConnect(host);
            }
        }
    }

    private void ConfigureConnection(string host)
    {
		context.Host = host;
        this.Connection = new HubConnectionBuilder()
            .WithUrl(this.context.Url)
            .Build();
        this.Connection.Closed += ex =>
        {
            this.BeginReconnecting(this.reconnectTokenSource.Token, ex);
            return Task.CompletedTask;
        };
        foreach (var registerProcedure in procedureRegistrations)
        {
            registerProcedure(this.Connection);
        }
    }

    public virtual async Task Disconnect()
    {
        if (this.Connection == null || !this.IsConnected)
        {
            return;
        }
		this.reconnectTokenSource.Cancel();
        await this.Connection.StopAsync();
    }

	public async ValueTask DisposeAsync()
    {
        if (this.Connection == null)
        {
            return;
        }
        await this.Connection.DisposeAsync();
		this.reconnectTokenSource.Dispose();
    }

	protected void AddProcedure(string name, Func<Task> action)
	{
		this.procedureRegistrations.Add(connection =>
		{
			connection.On(name, () =>
			{
				try
				{
					action();
				}
				catch (Exception exception)
				{
					this.RaiseError(exception, name);
				}
			});
		});
	}
	protected void AddProcedure<T>(string name, Func<T, Task> action)
	{
		this.procedureRegistrations.Add(connection =>
		{
			connection.On<T>(name, a =>
			{
				try
				{
					action(a);
				}
				catch (Exception exception)
				{
					this.RaiseError(exception, name, a);
				}
			});
		});
	}
    protected void AddProcedure<T1, T2>(string name, Func<T1, T2, Task> action)
    {
		this.procedureRegistrations.Add(connection =>
		{
			connection.On<T1, T2>(name, (a, b) =>
			{
				try
				{
					action(a, b);
				}
				catch (Exception exception)
				{
					this.RaiseError(exception, name, a, b);
				}
			});
		});
	}
    protected void AddProcedure<T1, T2, T3>(string name, Func<T1, T2, T3, Task> action)
    {
		this.procedureRegistrations.Add(connection =>
		{
			connection.On<T1, T2, T3>(name, (a, b, c) =>
			{
				try
				{
					action(a, b,c);
				}
				catch (Exception exception)
				{
					this.RaiseError(exception, name, a, b, c);
				}
			});
		});
	}

	protected async Task<RpcInvokeResult> InvokeAsync<T>(string name, T parameter)
	{
		try
		{
			await this.Connection.InvokeAsync(name, parameter);
			return RpcInvokeResult.Success;
		}
		catch (Exception exception)
		{
			this.RaiseError(exception, name, parameter);
			return RpcInvokeResult.Error;
		}
	}

	protected async Task<RpcInvokeResult> InvokeAsync<T1, T2>(string name, T1 parameter1, T2 parameter2)
	{
		try
		{
			await this.Connection.InvokeAsync(name, parameter1, parameter2);
			return RpcInvokeResult.Success;
		}
		catch (Exception exception)
		{
			this.RaiseError(exception, name, parameter1, parameter2);
			return RpcInvokeResult.Error;
		}
	}

	protected async Task<RpcInvokeResult<T>> InvokeAsync<T>(string name)
	{
		try
		{
			var result = await this.Connection.InvokeAsync<T>(name);
			return RpcInvokeResult<T>.Success(result);
		}
		catch (Exception exception)
		{
			this.RaiseError(exception, name);
			return RpcInvokeResult<T>.Error;
        }
	}

    public event EventHandler<RpcError>? Error;
    private void RaiseError(Exception exception, string? procedure, params object?[] arguments)
    {
		var message = procedure == null
			? $"RpcClient error : {exception.Message}"
			: $"RpcClient error in '{procedure}': {exception.Message}";
        Console.WriteLine(message);
        var error = new RpcError(exception, procedure, arguments);
        this.Error?.Invoke(this, error);
    }

    private void BeginReconnecting(CancellationToken cancellationToken, Exception? error)
    {
		this.RaiseDisconnected(error);
		RaiseConnecting();
        var timer = new System.Timers.Timer(TimeSpan.FromSeconds(5).TotalMilliseconds);
        timer.Elapsed += async (s, e) =>
        {
            if (cancellationToken.IsCancellationRequested)
            {
				ServerConnectionInfo?.Invoke(this, "Reconecting stopped due to cancelation request");
                timer.Stop();
                timer.Dispose();
            }
            try
            {
                await this.Connection!.StartAsync();
                if (this.Connection.State == HubConnectionState.Connected)
                {
					this.RaiseConnected();
                    timer.Stop();
                    timer.Dispose();
                }
            }
            catch (Exception ex) 
			{
				ServerConnectionInfo?.Invoke(this, $"Reconnect attempt failed: {ex.Message}");
			}
        };
        timer.Start();
    }

	private void RaiseDisconnected(Exception? ex = default)
	{
		ServerConnectionChanged?.Invoke(this, RpcConnectionStatus.Disconnected);
		ServerConnectionInfo?.Invoke(this, ex?.Message ?? "Disconnected manually");
	}
	private void RaiseConnecting()
	{
		ServerConnectionChanged?.Invoke(this, RpcConnectionStatus.Connecting);
        ServerConnectionInfo?.Invoke(this, "Attempting to connect");
    }
	private void RaiseConnected()
	{
		ServerConnectionChanged?.Invoke(this, RpcConnectionStatus.Connected);
        ServerConnectionInfo?.Invoke(this, "Connected");
    }
}

public interface IRpcClient
{
	/// <summary>
	/// 'true' means connected; 'false' - disconnected;
	/// </summary>
	event EventHandler<RpcConnectionStatus>? ServerConnectionChanged;
	event EventHandler<string>? ServerConnectionInfo;
	event EventHandler<RpcError>? Error;
	bool IsConnected { get; }
	Task Connect(string host);
    Task Disconnect();
}

public enum RpcConnectionStatus
{
	Disconnected = 0,
	Connecting = 1,
	Connected = 2,
}

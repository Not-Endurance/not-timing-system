using Core.Application.Rpc.Procedures;
using Core.Domain.AggregateRoots.Manager.Aggregates.Startlists;
using Microsoft.AspNetCore.SignalR.Client;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Application.Rpc;

public class RpcClient : IRpcClient, IAsyncDisposable
{
	private const int AUTOMATIC_RECONNECT_ATTEMPTS = 3;
    public event EventHandler<RpcConnectionStatus>? ServerConnectionChanged;
	public event EventHandler<string>? ServerConnectionInfo;
	public event EventHandler<RpcError>? Error;

	public bool IsConnected => this.Connection?.State == HubConnectionState.Connected;

    private readonly RpcContext context;

    // Necessary because this.Connection instance is not intialized 
    // when procedures are reigstered in the child constructor
    private List<Action<HubConnection>> procedureRegistrations = new();
	private CancellationTokenSource? reconnectTokenSource;
	private readonly string _name;

    public RpcClient(RpcContext context)
    {
        this.context = context;
		_name = GetType().Name;
    }
    
    protected HubConnection? Connection { get; private set; }

    public virtual async Task Connect(string host)
    {
        await InternalConnect(host, 0);
	}

    public virtual async Task Disconnect()
    {
        if (this.Connection == null || !this.IsConnected)
        {
            return;
        }
		this.reconnectTokenSource!.Cancel();
		reconnectTokenSource!.Dispose();
        await this.Connection.StopAsync();
    }

	public async ValueTask DisposeAsync()
    {
        if (this.Connection == null)
        {
            return;
        }
        await this.Connection.DisposeAsync();
		this.reconnectTokenSource?.Dispose();
    }

	private async Task InternalConnect(string host, int reconnectAttempts)
	{
		if (IsConnected)
		{
			ServerConnectionInfo?.Invoke(this, $"{this.GetType().Name} is already connected");
			return;
		}
		if (this.Connection == null)
		{
			this.ConfigureConnection(host);
		}
		try
		{
			this.reconnectTokenSource = new CancellationTokenSource();
			RaiseConnecting();
			await this.Connection!.StartAsync();
			this.RaiseConnected();
		}
		catch (Exception ex)
		{
			if (HasReachedReconnectionAttemptLimit(++reconnectAttempts))
			{
				RaiseDisconnected(ex);
				return;
			}
			await Task.Delay(TimeSpan.FromSeconds(5));
			await InternalConnect(host, reconnectAttempts);
		}
	}

	private void ConfigureConnection(string host)
	{
		var reconnectionAttempts = 0;
		context.Host = host;
		this.Connection = new HubConnectionBuilder()
			.WithUrl(this.context.Url)
			.Build();
		this.Connection.Closed += ex =>
		{
			// This check is also necessary here, because if the server hub cannot be constructed (DI error for example)
			// SignalR keeps closing each connection to that hub as soon as it is created
			// Maybe test again with static connection?
			if (!HasReachedReconnectionAttemptLimit(++reconnectionAttempts))
			{
				this.BeginReconnecting(this.reconnectTokenSource!.Token, ex);
			}
			return Task.CompletedTask;
		};
		foreach (var registerProcedure in procedureRegistrations)
		{
			registerProcedure(this.Connection);
		}
	}

    private void BeginReconnecting(CancellationToken cancellationToken, Exception? error)
    {
		this.RaiseDisconnected(error);
		RaiseConnecting();
		var reconnectAttempts = 0;
		var timer = new System.Timers.Timer(TimeSpan.FromSeconds(10).TotalMilliseconds);
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
				RaiseReconnecting(ex);
			}
			finally
			{
				if (HasReachedReconnectionAttemptLimit(++reconnectAttempts))
				{
					RaiseDisconnected(new Exception("Automatic reconnection reached attempt limits. Try to reconnect manually"));
					timer.Stop();
					timer.Dispose();
				}
			}
        };
        timer.Start();
    }

	private bool HasReachedReconnectionAttemptLimit(int attempts)
	{
		return attempts >= AUTOMATIC_RECONNECT_ATTEMPTS;
	}
	private void RaiseError(Exception exception, string? procedure, params object?[] arguments)
	{
		var message = procedure == null
			? $"RpcClient error : {exception.Message}"
			: $"RpcClient error in '{procedure}': {exception.Message}";
		Console.WriteLine(message);
		var error = new RpcError(exception, procedure, arguments);
		this.Error?.Invoke(this, error);
	}

	private void RaiseDisconnected(Exception? ex = default)
	{
		ServerConnectionChanged?.Invoke(_name, RpcConnectionStatus.Disconnected);
		ServerConnectionInfo?.Invoke(_name, ex?.Message ?? "Disconnected manually");
	}

	private void RaiseReconnecting(Exception ex)
	{
		ServerConnectionChanged?.Invoke(_name, RpcConnectionStatus.Connecting);
        ServerConnectionInfo?.Invoke(_name, $"{ex.Message}. Attempting to reconnect");
    }

	private void RaiseConnecting()
	{
		ServerConnectionChanged?.Invoke(_name, RpcConnectionStatus.Connecting);
    }

	private void RaiseConnected()
	{
		ServerConnectionChanged?.Invoke(_name, RpcConnectionStatus.Connected);
    }

	protected void RegisterClientProcedure(string name, Func<Task> action)
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
	protected void RegisterClientProcedure<T>(string name, Func<T, Task> action)
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
	protected void RegisterClientProcedure<T1, T2>(string name, Func<T1, T2, Task> action)
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
	protected void RegisterClientProcedure<T1, T2, T3>(string name, Func<T1, T2, T3, Task> action)
	{
		this.procedureRegistrations.Add(connection =>
		{
			connection.On<T1, T2, T3>(name, (a, b, c) =>
			{
				try
				{
					action(a, b, c);
				}
				catch (Exception exception)
				{
					this.RaiseError(exception, name, a, b, c);
				}
			});
		});
	}

	protected async Task<RpcInvokeResult> InvokeHubProcedure<T>(string name, T parameter)
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

	protected async Task<RpcInvokeResult> InvokeHubProcedure<T1, T2>(string name, T1 parameter1, T2 parameter2)
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

	protected async Task<RpcInvokeResult<T>> InvokeHubProcedure<T>(string name)
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

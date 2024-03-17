using Core.Application.Rpc.Procedures;
using Core.Domain.AggregateRoots.Manager;
using Core.Domain.AggregateRoots.Manager.Aggregates.Startlists;
using EMS.Judge.Api.Configuration;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EMS.Judge.Api.Rpc.Hubs;

public class StartlistHub : Hub<IStartlistClientProcedures>, IStartlistHubProcedures
{
	private readonly ManagerRoot managerRoot;

	public StartlistHub(IJudgeServiceProvider provider)
	{
		this.managerRoot = provider.GetRequiredService<ManagerRoot>();
	}
		
	public Dictionary<int, Startlist> Get()
	{
		return new Dictionary<int, Startlist>();
		var startlist = this.managerRoot.GetStartList();
		return startlist;
	}

	public override async Task OnConnectedAsync()
	{
		Console.WriteLine($"Connected: {this.Context.ConnectionId}");
	}

	public override async Task OnDisconnectedAsync(Exception exception)
	{
		Console.WriteLine($"Disconnected: {this.Context.ConnectionId}");
	}
}

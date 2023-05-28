﻿using Core.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EMS.Judge.Api.Services;

public class NetworkBroadcastService : BackgroundService
{
    private readonly INetworkBroadcastService networkService;
    public NetworkBroadcastService(INetworkBroadcastService networkService)
    {
        this.networkService = networkService;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("Broadcasting");
        try
        {
            return this.networkService.StartBroadcasting(stoppingToken);
        }
        catch (Exception exception)
        {
            Console.WriteLine("Error while broadcasting!");
            Console.WriteLine(exception.Message);
            Console.WriteLine(exception.StackTrace);
        }
        return Task.CompletedTask;
    }
}

using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Not.Application.RPC;
using Not.Application.RPC.SignalR;
using Not.Notify;
using NTS.Application;
using NTS.Application.Watcher;
using NTS.Nexus.Warp.Features;
using NTS.Nexus.Warp.Features.Judge;
using NTS.Nexus.Warp.Features.Witness;

namespace NTS.Judge.Tests.Warp;

public class JudgeHubConnectionTimeoutIntegrationTests
{
    [Fact]
    public async Task Judge_connect_times_out_when_negotiate_is_slow()
    {
        var port = GetFreePort();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        builder.Services.AddLogging();
        builder.Services.AddSignalR();
        builder.Services.AddSingleton<JudgeConnectionsContext>();
        builder.Services.AddSingleton<IPendingSnapshotsService>(
            new EmptyPendingSnapshotsService()
        );

        await using var app = builder.Build();
        app.Use(
            async (context, next) =>
            {
                if (
                    context.Request.Path.StartsWithSegments($"/{ApplicationConstants.JUDGE_HUB}/negotiate")
                )
                {
                    await Task.Delay(TimeSpan.FromSeconds(2));
                }

                await next();
            }
        );
        app.MapHub<JudgeRpcHub>(ApplicationConstants.JUDGE_HUB);
        await app.StartAsync();

        var notifier = new RecordingNotifier();
        await using var socket = new SignalRSocket(
            Options.Create(
                new RpcSettings
                {
                    Host = $"http://127.0.0.1:{port}",
                    HubPattern = ApplicationConstants.JUDGE_HUB,
                    ConnectTimeoutSeconds = 1,
                    ClientName = "JudgeIntegrationTests",
                    AppVersion = "1.0.0-test",
                }
            ),
            notifier
        );
        var statuses = new List<SocketConnectionStatus>();
        var infoMessages = new List<string>();
        socket.ServerConnectionChanged += (_, status) => statuses.Add(status);
        socket.ServerConnectionInfo += (_, info) => infoMessages.Add(info);

        await socket.Connect("14");

        Assert.Equal([SocketConnectionStatus.Connecting, SocketConnectionStatus.Disconnected], statuses);
        Assert.Contains(infoMessages, x => x.Contains("timed out", StringComparison.OrdinalIgnoreCase));
        Assert.Null(socket.Connection);
        Assert.Empty(notifier.Errors);

        await app.StopAsync();
    }

    static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    sealed class EmptyPendingSnapshotsService : IPendingSnapshotsService
    {
        public Task Append(string enduranceEventId, SnapshotGroupModel snapshotGroup)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PendingSnapshotsModel>> Read(string enduranceEventId)
        {
            return Task.FromResult<IReadOnlyList<PendingSnapshotsModel>>([]);
        }

        public Task Remove(PendingSnapshotsModel pendingSnapshots)
        {
            return Task.CompletedTask;
        }
    }

    sealed class RecordingNotifier : INotifier
    {
        public List<Exception> Errors { get; } = [];

        public void Inform(string message) { }

        public void Success(string message) { }

        public void Warn(string message) { }

        public void Error(string message) { }

        public void Error(Exception ex)
        {
            Errors.Add(ex);
        }
    }
}

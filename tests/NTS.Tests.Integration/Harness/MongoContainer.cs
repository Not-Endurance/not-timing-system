using System.Net;
using Docker.DotNet;
using Testcontainers.MongoDb;

namespace NTS.Tests.Integration.Harness;

/// <summary>
/// A MongoDB container for a fixture, which takes its volumes with it when it is disposed. MongoDB keeps its data in a volume
/// that the image declares, and Testcontainers removes a container without that volume, so every fixture of a run left its
/// volumes on the disk (19 GB in one run of the suite). A container that does not start is released the same way.
/// </summary>
internal sealed class MongoContainer : IAsyncDisposable
{
    public static async Task<MongoContainer> StartAsync()
    {
        var container = new MongoDbBuilder().WithImage("mongo:6.0").Build();
        try
        {
            await container.StartAsync();
        }
        catch
        {
            await ReleaseAsync(container);
            throw;
        }

        return new MongoContainer(container);
    }

    readonly MongoDbContainer _container;

    MongoContainer(MongoDbContainer container)
    {
        _container = container;
    }

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask DisposeAsync()
    {
        await ReleaseAsync(_container);
    }

    static async Task ReleaseAsync(MongoDbContainer container)
    {
        using var docker = new DockerClientConfiguration().CreateClient();

        IReadOnlyList<string> volumes;
        try
        {
            volumes = await VolumesOfAsync(docker, container.Id);
        }
        catch (DockerApiException)
        {
            volumes = [];
        }

        await container.DisposeAsync();

        foreach (var volume in volumes)
        {
            await RemoveVolumeAsync(docker, volume);
        }
    }

    static async Task<IReadOnlyList<string>> VolumesOfAsync(IDockerClient docker, string? containerId)
    {
        if (string.IsNullOrEmpty(containerId))
        {
            return [];
        }

        var inspected = await docker.Containers.InspectContainerAsync(containerId);
        return [.. inspected.Mounts.Where(x => x.Type == "volume").Select(x => x.Name)];
    }

    static async Task RemoveVolumeAsync(IDockerClient docker, string volume)
    {
        try
        {
            await docker.Volumes.RemoveAsync(volume, force: true);
        }
        catch (DockerApiException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            // Already removed with its container, which Testcontainers does in some versions.
        }
    }
}

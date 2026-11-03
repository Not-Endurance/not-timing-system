using System.Net;
using Docker.DotNet;
using DotNet.Testcontainers.Containers;
using Testcontainers.MongoDb;

namespace NTS.Tests.Integration.Harness;

/// <summary>
/// The MongoDB container of a test run, which serves every class of the run (ADR-0016), and which takes its volumes with it
/// when it is disposed. MongoDB keeps its data in a volume that the image declares, and Testcontainers removes a container
/// without that volume, so every container of a run left its volumes on the disk (19 GB in one run of the suite). A container
/// that does not start is released the same way.
/// </summary>
internal sealed class MongoContainer : IAsyncDisposable
{
    const int STARTS = 3;

    /// <summary>
    /// Starts the container of the run. The image's entrypoint starts a temporary mongod on the port before the real one, and
    /// the real one can find the port still taken and exit with code 48: that happened once in about 150 starts. The start
    /// is made again in that case, at most <see cref="STARTS"/> times. This repeats the container's start and not a test, which
    /// ADR-0016 allows no retry of.
    /// </summary>
    public static async Task<MongoContainer> StartAsync()
    {
        for (var attempt = 1; ; attempt++)
        {
            var container = new MongoDbBuilder().WithImage("mongo:6.0").Build();
            try
            {
                await container.StartAsync();
                return new MongoContainer(container);
            }
            catch (ContainerNotRunningException) when (attempt < STARTS)
            {
                await ReleaseAsync(container);
                Console.Error.WriteLine(
                    $"The MongoDB container exited while it started (attempt {attempt} of {STARTS}); starting it again."
                );
            }
            catch
            {
                await ReleaseAsync(container);
                throw;
            }
        }
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
        using var docker = new DockerClientBuilder().Build();

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

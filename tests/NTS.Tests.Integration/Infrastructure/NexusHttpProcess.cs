using System.ComponentModel;
using System.Diagnostics;

namespace NTS.Tests.Integration.Infrastructure;

internal sealed class NexusHttpProcess : IAsyncDisposable
{
    readonly RepositoryPaths _paths;
    readonly ProcessOutputCollector _output = new();
    Process? _process;

    public NexusHttpProcess(RepositoryPaths paths, int port, string mongoConnectionString)
    {
        _paths = paths;
        Port = port;
        MongoConnectionString = mongoConnectionString;
        BaseUrl = new Uri($"http://127.0.0.1:{Port}");
    }

    static string BuildConfiguration
#if DEBUG
            => "Debug";
#else
            => "Release";
#endif

    public int Port { get; }
    public string MongoConnectionString { get; }
    public Uri BaseUrl { get; }

    public async Task Start(CancellationToken cancellationToken = default)
    {
        var dotnetHome = Environment.GetEnvironmentVariable("DOTNET_CLI_HOME") ?? _paths.DotnetHome;
        var nugetPackages = Environment.GetEnvironmentVariable("NUGET_PACKAGES") ?? _paths.NuGetPackages;
        Directory.CreateDirectory(dotnetHome);
        Directory.CreateDirectory(nugetPackages);

        var outputDirectory = _paths.GetNexusHttpOutputDirectory(BuildConfiguration);
        if (!File.Exists(Path.Combine(outputDirectory, "functions.metadata")))
        {
            throw new InvalidOperationException(
                $"Nexus HTTP build output was not found at '{outputDirectory}'. Build the integration test project before running with --no-build."
            );
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "func",
            WorkingDirectory = outputDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("start");
        startInfo.ArgumentList.Add("--port");
        startInfo.ArgumentList.Add(Port.ToString());
        startInfo.ArgumentList.Add("--no-build");

        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.Environment["AZURE_FUNCTIONS_ENVIRONMENT"] = "Development";
        startInfo.Environment["FUNCTIONS_WORKER_RUNTIME"] = "dotnet-isolated";
        startInfo.Environment["AzureWebJobsStorage"] = "UseDevelopmentStorage=true";
        startInfo.Environment["ConnectionStrings__MongoDB"] = MongoConnectionString;
        startInfo.Environment["IS_LOCALHOST"] = "true";
        startInfo.Environment["DOTNET_CLI_HOME"] = dotnetHome;
        startInfo.Environment["NUGET_PACKAGES"] = nugetPackages;

        try
        {
            _process = Process.Start(startInfo);
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException(
                "Failed to start Nexus HTTP. Install Azure Functions Core Tools v4 and make sure 'func' is on PATH.",
                ex
            );
        }

        if (_process == null)
        {
            throw new InvalidOperationException("Failed to start Nexus HTTP process.");
        }

        _process.OutputDataReceived += (_, args) => _output.Add("nexus-http", args.Data);
        _process.ErrorDataReceived += (_, args) => _output.Add("nexus-http-error", args.Data);
        _process.BeginOutputReadLine();
        _process.BeginErrorReadLine();

        await WaitUntilHealthy(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_process == null)
        {
            return;
        }

        try
        {
            if (!_process.HasExited)
            {
                _process.Kill(entireProcessTree: true);
                await _process.WaitForExitAsync();
            }
        }
        finally
        {
            _process.Dispose();
        }
    }

    async Task WaitUntilHealthy(CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(90);
        using var http = new HttpClient { BaseAddress = BaseUrl };

        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_process?.HasExited == true)
            {
                throw new InvalidOperationException(
                    $"Nexus HTTP exited before becoming healthy with exit code {_process.ExitCode}.{Environment.NewLine}{_output.Dump()}"
                );
            }

            try
            {
                using var response = await http.GetAsync("/api/event-information", cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch when (!cancellationToken.IsCancellationRequested) { }

            await Task.Delay(250, cancellationToken);
        }

        throw new TimeoutException($"Nexus HTTP did not become healthy at {BaseUrl}.{Environment.NewLine}{_output.Dump()}");
    }
}

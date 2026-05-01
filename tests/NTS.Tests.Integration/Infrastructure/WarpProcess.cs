using System.Diagnostics;

namespace NTS.Tests.Integration.Infrastructure;

internal sealed class WarpProcess : IAsyncDisposable
{
    readonly RepositoryPaths _paths;
    readonly ProcessOutputCollector _output = new();
    Process? _process;

    public WarpProcess(RepositoryPaths paths, int port, string mongoConnectionString)
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

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = _paths.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("run");
        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(_paths.WarpProject);
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add(BuildConfiguration);
        startInfo.ArgumentList.Add("--no-build");
        startInfo.ArgumentList.Add("--no-launch-profile");

        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.Environment["PORT"] = Port.ToString();
        startInfo.Environment["MONGO_CONNECTION_STRING"] = MongoConnectionString;
        startInfo.Environment["DOTNET_CLI_HOME"] = dotnetHome;
        startInfo.Environment["NUGET_PACKAGES"] = nugetPackages;

        _process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start Warp process.");
        _process.OutputDataReceived += (_, args) => _output.Add("warp", args.Data);
        _process.ErrorDataReceived += (_, args) => _output.Add("warp-error", args.Data);
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
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(45));
        using var http = new HttpClient { BaseAddress = BaseUrl };

        while (!timeout.IsCancellationRequested)
        {
            if (_process?.HasExited == true)
            {
                throw new InvalidOperationException(
                    $"Warp exited before becoming healthy with exit code {_process.ExitCode}.{Environment.NewLine}{_output.Dump()}"
                );
            }

            try
            {
                using var response = await http.GetAsync("/healthz", timeout.Token);
                if (response.IsSuccessStatusCode)
                {
                    return;
                }
            }
            catch when (!timeout.IsCancellationRequested) { }

            await Task.Delay(250, timeout.Token);
        }

        throw new TimeoutException($"Warp did not become healthy at {BaseUrl}.{Environment.NewLine}{_output.Dump()}");
    }
}

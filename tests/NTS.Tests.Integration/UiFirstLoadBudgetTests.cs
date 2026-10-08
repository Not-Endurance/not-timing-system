using System.Diagnostics;
using NTS.Tests.Integration.Infrastructure;
using Xunit.Abstractions;

namespace NTS.Tests.Integration;

/// <summary>
/// The size of what a visitor downloads to open the app (#645), measured on what is delivered: the Ui published for Release,
/// the files the Api serves as Brotli. A debug build is several times larger and says nothing about it. The budget is a
/// ceiling the app stays under, set above what it was measured at when it was written down; a change that raises the size
/// past it raises it on purpose, and says so by raising the ceiling here, in the Readme of the Ui and in the ticket.
/// </summary>
public sealed class UiFirstLoadBudgetTests
{
    /// <summary>
    /// Measured at 8.29 MB (Brotli, 180 files, 28.3 MB as they are) when it was written down: 6.8 MB of assemblies, 0.9 MB of
    /// runtime, 0.3 MB of the globalization data that the choice of Bulgarian or Turkish needs, and 0.2 MB of scripts and styles.
    /// </summary>
    const long BUDGET_BYTES = 9 * 1024 * 1024;

    readonly ITestOutputHelper _output;

    public UiFirstLoadBudgetTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task What_a_visitor_downloads_to_open_the_app_stays_within_its_budget()
    {
        var paths = RepositoryPaths.Discover();
        var output = Path.Combine(paths.Root, ".tmp", "integration-ui-publish", Guid.NewGuid().ToString("N"));
        try
        {
            await PublishAsync(paths, output);

            var sent = SentFiles(Path.Combine(output, "wwwroot"));
            var total = sent.Sum(x => x.Bytes);
            foreach (var biggest in sent.OrderByDescending(x => x.Bytes).Take(12))
            {
                _output.WriteLine($"{biggest.Bytes / 1024, 7} KB  {biggest.Name}");
            }

            _output.WriteLine(
                $"{sent.Count} files, {total / 1024.0 / 1024.0:0.00} MB (budget {BUDGET_BYTES / 1024.0 / 1024.0:0.00} MB)"
            );
            Assert.True(
                total <= BUDGET_BYTES,
                $"The app is {total / 1024.0 / 1024.0:0.00} MB to download, over its budget of {BUDGET_BYTES / 1024.0 / 1024.0:0.00} MB."
            );
            Assert.DoesNotContain(sent, x => x.Name.Contains("Msal", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    /// <summary>The files of the published app as they are sent: the Brotli file where there is one, and the file itself where there is none.</summary>
    static List<(string Name, long Bytes)> SentFiles(string wwwroot)
    {
        var files = Directory.EnumerateFiles(wwwroot, "*", SearchOption.AllDirectories).ToList();
        var compressed = files
            .Where(x => x.EndsWith(".br", StringComparison.Ordinal))
            .ToDictionary(x => x[..^3], x => new FileInfo(x).Length);
        return
        [
            .. files
                .Where(x =>
                    !x.EndsWith(".br", StringComparison.Ordinal) && !x.EndsWith(".gz", StringComparison.Ordinal)
                )
                .Select(x =>
                    (
                        Path.GetRelativePath(wwwroot, x).Replace('\\', '/'),
                        compressed.TryGetValue(x, out var bytes) ? bytes : new FileInfo(x).Length
                    )
                ),
        ];
    }

    static async Task PublishAsync(RepositoryPaths paths, string output)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            WorkingDirectory = paths.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (
            var argument in new[] { "publish", "src/NoTiming.Ui/NoTiming.Ui.csproj", "-c", "Release", "-o", output }
        )
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.ArgumentList.Add("-v:q");
        startInfo.ArgumentList.Add("-nr:false");
        startInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        var collected = new ProcessOutputCollector();
        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("dotnet did not start.");
        process.OutputDataReceived += (_, e) => collected.Add("out", e.Data);
        process.ErrorDataReceived += (_, e) => collected.Add("err", e.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        await process.WaitForExitAsync(timeout.Token);

        Assert.True(process.ExitCode == 0, $"The Ui did not publish:{Environment.NewLine}{collected.Dump()}");
    }
}

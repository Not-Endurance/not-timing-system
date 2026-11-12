using System.Diagnostics;

namespace NoTiming.IntegrationTests.Infrastructure;

/// <summary>Runs the database migrations the way CD does: <c>npm run migrate:up</c> in <c>database</c>, with migrate-mongo.</summary>
internal static class MigrateMongo
{
    public static async Task UpAsync(RepositoryPaths paths, string mongoConnectionString)
    {
        var database = Path.Combine(paths.Root, "database");
        if (!Directory.Exists(Path.Combine(database, "node_modules")))
        {
            await RunNpmAsync(database, mongoConnectionString, "ci", "--no-audit", "--no-fund");
        }

        await RunNpmAsync(database, mongoConnectionString, "run", "migrate:up");
    }

    static async Task RunNpmAsync(string database, string mongoConnectionString, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            WorkingDirectory = database,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        if (OperatingSystem.IsWindows())
        {
            startInfo.FileName = "cmd.exe";
            startInfo.ArgumentList.Add("/c");
            startInfo.ArgumentList.Add("npm");
        }
        else
        {
            startInfo.FileName = "npm";
        }

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["MONGO_CONNECTION_STRING"] = mongoConnectionString;

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start npm.");
        var standardOutput = process.StandardOutput.ReadToEndAsync();
        var standardError = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"npm {string.Join(' ', arguments)} failed with exit code {process.ExitCode}.{Environment.NewLine}"
                    + $"{await standardOutput}{Environment.NewLine}{await standardError}"
            );
        }
    }
}

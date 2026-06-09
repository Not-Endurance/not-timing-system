using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NTS.Tests.Integration.Infrastructure;

internal static class PlaywrightBrowserEnvironment
{
    const string PLAYWRIGHT_BROWSERS_PATH = "PLAYWRIGHT_BROWSERS_PATH";
    const string NTS_INTEGRATION_PLAYWRIGHT_BROWSERS_PATH = "NTS_INTEGRATION_PLAYWRIGHT_BROWSERS_PATH";

    public static void Configure(ProcessStartInfo startInfo, RepositoryPaths paths)
    {
        var resolution = Resolve(paths);
        if (resolution.BrowsersPath != null)
        {
            startInfo.Environment[PLAYWRIGHT_BROWSERS_PATH] = resolution.BrowsersPath;
            return;
        }

        if (resolution.RemoveInheritedBrowsersPath)
        {
            startInfo.Environment.Remove(PLAYWRIGHT_BROWSERS_PATH);
        }
    }

    static Resolution Resolve(RepositoryPaths paths)
    {
        var explicitPath = Environment.GetEnvironmentVariable(NTS_INTEGRATION_PLAYWRIGHT_BROWSERS_PATH);
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            var normalized = NormalizePath(explicitPath);
            if (HasCurrentPlatformChromium(normalized))
            {
                return new Resolution(normalized, RemoveInheritedBrowsersPath: false);
            }

            throw new InvalidOperationException(
                $"{NTS_INTEGRATION_PLAYWRIGHT_BROWSERS_PATH} points to '{explicitPath}', but no Chromium browser for {CurrentPlatformName()} was found there."
            );
        }

        var inheritedPath = Environment.GetEnvironmentVariable(PLAYWRIGHT_BROWSERS_PATH);
        if (!string.IsNullOrWhiteSpace(inheritedPath))
        {
            if (IsSpecialPlaywrightValue(inheritedPath))
            {
                return new Resolution(inheritedPath, RemoveInheritedBrowsersPath: false);
            }

            var normalized = NormalizePath(inheritedPath);
            if (HasCurrentPlatformChromium(normalized))
            {
                return new Resolution(normalized, RemoveInheritedBrowsersPath: false);
            }
        }

        foreach (var candidate in CandidatePaths(paths))
        {
            if (HasCurrentPlatformChromium(candidate))
            {
                return new Resolution(candidate, RemoveInheritedBrowsersPath: true);
            }
        }

        return new Resolution(BrowsersPath: null, RemoveInheritedBrowsersPath: !string.IsNullOrWhiteSpace(inheritedPath));
    }

    static IEnumerable<string> CandidatePaths(RepositoryPaths paths)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            yield return "/ms-playwright";
        }

        yield return Path.Combine(paths.Root, ".tools", "ms-playwright");
        yield return Path.Combine(paths.Root, ".tmp", "ms-playwright");
    }

    static bool HasCurrentPlatformChromium(string browsersPath)
    {
        if (!Directory.Exists(browsersPath))
        {
            return false;
        }

        return CurrentPlatformExecutableNames().Any(executableName =>
            Directory
                .EnumerateFiles(browsersPath, executableName, SearchOption.AllDirectories)
                .Any(IsCurrentPlatformChromiumExecutable)
        );
    }

    static bool IsCurrentPlatformChromiumExecutable(string file)
    {
        var parentDirectory = Path.GetFileName(Path.GetDirectoryName(file)) ?? string.Empty;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return parentDirectory.StartsWith("chrome-win", StringComparison.OrdinalIgnoreCase)
                || parentDirectory.StartsWith("chrome-headless-shell-win", StringComparison.OrdinalIgnoreCase);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return parentDirectory.StartsWith("chrome-linux", StringComparison.OrdinalIgnoreCase)
                || parentDirectory.StartsWith("chrome-headless-shell-linux", StringComparison.OrdinalIgnoreCase);
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return file.Contains($"{Path.DirectorySeparatorChar}chrome-mac", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    static string[] CurrentPlatformExecutableNames()
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? ["chrome-headless-shell.exe", "chrome.exe"]
            : ["chrome-headless-shell", "chrome"];
    }

    static bool IsSpecialPlaywrightValue(string value)
    {
        return value == "0";
    }

    static string NormalizePath(string path)
    {
        var expanded = Environment.ExpandEnvironmentVariables(path);
        if (expanded.StartsWith($"~{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrWhiteSpace(home))
            {
                expanded = Path.Combine(home, expanded[2..]);
            }
        }

        return Path.GetFullPath(expanded);
    }

    static string CurrentPlatformName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "Windows";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "Linux";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "macOS";
        }

        return RuntimeInformation.OSDescription;
    }

    readonly record struct Resolution(string? BrowsersPath, bool RemoveInheritedBrowsersPath);
}

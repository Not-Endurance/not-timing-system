using System.Reflection;
using Microsoft.AspNetCore.Components.WebAssembly.Services;
using Not.Injection;

namespace NoTiming.Ui.Features.Lazy;

/// <summary>The assemblies of the app that are fetched when a person goes to the pages that need them (<see cref="LazyRoutes"/>).</summary>
public interface ILazyAssemblies
{
    /// <summary>The assemblies fetched so far, which the router looks for pages in beside the app's own.</summary>
    IReadOnlyList<Assembly> Loaded { get; }

    /// <summary>Fetches what the path needs and has not been fetched, before the router looks for a page.</summary>
    Task LoadFor(string path);
}

public sealed class BlazorLazyAssemblies : ILazyAssemblies, IScoped
{
    readonly LazyAssemblyLoader _loader;
    readonly HashSet<string> _files = [];
    readonly List<Assembly> _loaded = [];

    public BlazorLazyAssemblies(LazyAssemblyLoader loader)
    {
        _loader = loader;
    }

    public IReadOnlyList<Assembly> Loaded => _loaded;

    public async Task LoadFor(string path)
    {
        var needed = LazyRoutes.NeededBy(path, LazyRoutes.Assemblies).Where(file => !_files.Contains(file)).ToList();
        if (needed.Count == 0)
        {
            return;
        }

        _loaded.AddRange(await _loader.LoadAssembliesAsync(needed));
        _files.UnionWith(needed);
    }
}

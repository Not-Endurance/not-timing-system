namespace NTS.Tests.Integration.Infrastructure;

/// <summary>
/// The Api alone, on a real loopback port. It needs no Docker: the Api has no database of its own yet, so the tests of
/// the host, its hub and its headers run anywhere the solution builds.
/// </summary>
public sealed class ApiHostFixture : IAsyncLifetime
{
    ApiFactory? _api;

    internal ApiFactory Api => _api ?? throw new InvalidOperationException("The Api is not started.");
    public Uri BaseAddress => Api.BaseAddress;

    public Task InitializeAsync()
    {
        _api = new ApiFactory(kestrel: true);
        _ = _api.BaseAddress; // starts the host
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_api != null)
        {
            await _api.DisposeAsync();
        }
    }
}

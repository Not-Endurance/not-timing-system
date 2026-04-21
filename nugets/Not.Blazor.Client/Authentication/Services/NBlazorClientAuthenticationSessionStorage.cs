using System.Globalization;
using Microsoft.Extensions.Logging;
using Not.Injection;
using Not.Application.Authentication.Abstractions;
using Not.BLazor.Client.Browser;

namespace Not.Blazor.Client.Authentication.Services;

internal class NBlazorClientAuthenticationSessionStorage : INAuthenticationSessionStorage, ITransient
{
    const string SESSION_STARTED_AT_KEY = "not.auth.session-started-at";
    const string SIGNIN_FLOW_STARTED_AT_KEY = "not.auth.signin-flow-started-at";

    readonly IBrowserLocalStorage _browserLocalStorage;
    readonly ILogger<NBlazorClientAuthenticationSessionStorage> _logger;

    public NBlazorClientAuthenticationSessionStorage(IBrowserLocalStorage browserLocalStorage, ILogger<NBlazorClientAuthenticationSessionStorage> logger)
    {
        _browserLocalStorage = browserLocalStorage;
        _logger = logger;
    }

    public async Task<DateTimeOffset?> ReadSessionStartedAtAsync()
    {
        return await ReadTimestampAsync(SESSION_STARTED_AT_KEY);
    }

    public async Task WriteSessionStartedAt(DateTimeOffset startedAtUtc)
    {
        await WriteTimestamp(SESSION_STARTED_AT_KEY, startedAtUtc);
    }

    public async Task ClearSessionStartedAt()
    {
        await _browserLocalStorage.Clear(SESSION_STARTED_AT_KEY);
    }

    public async Task WriteSigninFlowStartedAt()
    {
        // Keep the in-progress sign-in marker across refreshes so the same login round-trip can finish.
        await WriteTimestamp(SIGNIN_FLOW_STARTED_AT_KEY, DateTimeOffset.UtcNow);
    }

    public async Task<DateTimeOffset?> ReadSigninFlowStartedAtAsync()
    {
        return await ReadTimestampAsync(SIGNIN_FLOW_STARTED_AT_KEY);
    }

    public async Task ClearSigninFlowStartedAt()
    {
        await _browserLocalStorage.Clear(SIGNIN_FLOW_STARTED_AT_KEY);
    }

    async Task<DateTimeOffset?> ReadTimestampAsync(string key)
    {
        try
        {
            var rawValue = await _browserLocalStorage.Read(key);
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return null;
            }

            if (!long.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixMilliseconds))
            {
                _logger.LogWarning("Client auth: Unable to parse stored timestamp for key {Key}.", key);
                return null;
            }

            return DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Client auth: Unable to read timestamp for key {Key}.", key);
            return null;
        }
    }

    async Task WriteTimestamp(string key, DateTimeOffset value)
    {
        var valueString = value.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        await _browserLocalStorage.Write(key, valueString);
    }

    
}

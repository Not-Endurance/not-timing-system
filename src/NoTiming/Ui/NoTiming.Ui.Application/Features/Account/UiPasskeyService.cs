using System.Text.Json.Nodes;
using NC.Application.HTTP;
using NC.Injection;
using NC.Serialization.JSON;
using NoTiming.Ui.Contracts.Features.Account;

namespace NoTiming.Ui.Application.Features.Account;

public sealed class UiPasskeyService : IUiPasskeyService, IScoped
{
    const string SIGN_IN_OPTIONS = "sign-in/passkey/options";
    const string SIGN_IN_VERIFY = "sign-in/passkey/verify";
    const string ENROL_OPTIONS = "me/passkey/options";
    const string PASSKEYS = "me/passkeys";

    readonly NHttpClient _client;

    public UiPasskeyService(NHttpClient client)
    {
        _client = client;
    }

    public Task<string> SignInOptionsAsync()
    {
        return _client.SendRawJson(HttpMethod.Post, SIGN_IN_OPTIONS);
    }

    public async Task SignInAsync(string credentialJson)
    {
        await _client.SendRawJson(HttpMethod.Post, SIGN_IN_VERIFY, credentialJson);
    }

    public Task<string> EnrolOptionsAsync()
    {
        return _client.SendRawJson(HttpMethod.Post, ENROL_OPTIONS);
    }

    public async Task EnrolAsync(string credentialJson, string? name)
    {
        var body = new JsonObject
        {
            ["name"] = name,
            ["credential"] = JsonNode.Parse(credentialJson),
        };
        await _client.SendRawJson(HttpMethod.Post, PASSKEYS, body.ToJsonString());
    }

    public async Task<IReadOnlyList<UiPasskey>> ListAsync()
    {
        var json = await _client.SendRawJson(HttpMethod.Get, PASSKEYS);
        return json.FromJson<UiPasskey[]>();
    }

    public async Task RenameAsync(string id, string name)
    {
        await _client.SendRawJson(HttpMethod.Patch, $"{PASSKEYS}/{id}", new { name }.ToJson());
    }

    public async Task RemoveAsync(string id)
    {
        await _client.SendRawJson(HttpMethod.Delete, $"{PASSKEYS}/{id}");
    }
}

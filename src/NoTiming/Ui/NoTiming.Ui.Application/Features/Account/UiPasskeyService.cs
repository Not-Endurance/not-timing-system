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

    public async Task<string> SignInOptionsAsync()
    {
        return (await _client.PostJson(SIGN_IN_OPTIONS)).Data!;
    }

    public async Task SignInAsync(string credentialJson)
    {
        await _client.PostJson(SIGN_IN_VERIFY, credentialJson);
    }

    public async Task<string> EnrolOptionsAsync()
    {
        return (await _client.PostJson(ENROL_OPTIONS)).Data!;
    }

    public async Task EnrolAsync(string credentialJson, string? name)
    {
        var body = new JsonObject { ["name"] = name, ["credential"] = JsonNode.Parse(credentialJson) };
        await _client.PostJson(PASSKEYS, body.ToJsonString());
    }

    public async Task<IReadOnlyList<UiPasskey>> ListAsync()
    {
        var json = (await _client.GetJson(PASSKEYS)).Data!;
        return json.FromJson<UiPasskey[]>();
    }

    public async Task RenameAsync(string id, string name)
    {
        await _client.PatchJson($"{PASSKEYS}/{id}", new { name }.ToJson());
    }

    public async Task RemoveAsync(string id)
    {
        await _client.DeleteJson($"{PASSKEYS}/{id}");
    }
}

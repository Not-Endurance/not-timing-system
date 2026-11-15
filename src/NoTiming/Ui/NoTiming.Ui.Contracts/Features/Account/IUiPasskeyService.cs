namespace NoTiming.Ui.Contracts.Features.Account;

/// <summary>
/// Passkeys: signing in with one, and the passkeys of the signed-in user. The credentials are the WebAuthn JSON the
/// browser produced. A refused request raises an HttpRequestException carrying its status.
/// </summary>
public interface IUiPasskeyService
{
    Task<string> SignInOptionsAsync();

    Task SignInAsync(string credentialJson);

    Task<string> EnrolOptionsAsync();

    Task EnrolAsync(string credentialJson, string? name);

    Task<IReadOnlyList<UiPasskey>> ListAsync();

    Task RenameAsync(string id, string name);

    Task RemoveAsync(string id);
}

public sealed class UiPasskey
{
    public string Id { get; set; } = string.Empty;

    public string? Name { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

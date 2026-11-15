using System.Net;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NC.Blazor.Components.Abstractions;
using NoTiming.Ui.Contracts.Features.Account;

namespace NoTiming.Ui.Blazor.Features.Authentication;

public class PasskeysContentBehind : NComponent
{
    [Inject]
    IUiPasskeyService PasskeyService { get; set; } = default!;

    [Inject]
    IJSRuntime JavaScript { get; set; } = default!;

    protected IReadOnlyList<UiPasskey> Passkeys { get; private set; } = [];

    protected bool NotSignedIn { get; private set; }

    protected string? Renaming { get; private set; }

    protected string NewName { get; set; } = string.Empty;

    protected string NewPasskeyName { get; set; } = string.Empty;

    protected string? Message { get; private set; }

    protected override async Task OnInitializedAsync()
    {
        await LoadSafe();
    }

    protected async Task AddPasskeySafe()
    {
        Message = null;
        try
        {
            if (!await PasskeyCeremony.IsSupportedAsync(JavaScript))
            {
                Message = Browser_cannot_use_passkeys_string;
                return;
            }

            var options = await PasskeyService.EnrolOptionsAsync();
            var credential = await PasskeyCeremony.CreateAsync(JavaScript, options);
            var name = string.IsNullOrWhiteSpace(NewPasskeyName) ? null : NewPasskeyName.Trim();
            await PasskeyService.EnrolAsync(credential, name);
            NewPasskeyName = string.Empty;
            await LoadSafe();
        }
        catch (JSException)
        {
            Message = Passkey_not_added_string;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            NotSignedIn = true;
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected void StartRename(UiPasskey passkey)
    {
        Renaming = passkey.Id;
        NewName = passkey.Name ?? string.Empty;
    }

    protected void CancelRename()
    {
        Renaming = null;
    }

    protected async Task RenameSafe(string id)
    {
        Message = null;
        try
        {
            await PasskeyService.RenameAsync(id, NewName.Trim());
            Renaming = null;
            await LoadSafe();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task RemoveSafe(string id)
    {
        Message = null;
        try
        {
            await PasskeyService.RemoveAsync(id);
            await LoadSafe();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            Message = Last_passkey_string;
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    async Task LoadSafe()
    {
        try
        {
            Passkeys = await PasskeyService.ListAsync();
            NotSignedIn = false;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            NotSignedIn = true;
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}

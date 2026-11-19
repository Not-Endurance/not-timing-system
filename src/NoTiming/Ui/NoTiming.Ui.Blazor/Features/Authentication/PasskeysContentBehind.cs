using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using NC.Blazor.Client.Authentication;
using NC.Blazor.Components.Abstractions;
using NC.Blazor.Dialogs;
using NC.Blazor.Helpers;
using NoTiming.Ui.Contracts.Features.Account;

namespace NoTiming.Ui.Blazor.Features.Authentication;

public class PasskeysContentBehind : NComponent
{
    [Inject]
    IUiPasskeyService PasskeyService { get; set; } = default!;

    [Inject]
    IJSRuntime JavaScript { get; set; } = default!;

    [Inject]
    IDialogService DialogService { get; set; } = default!;

    [Inject]
    NavigationManager Navigator { get; set; } = default!;

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
        var description = NewPasskeyName.Trim();
        if (description.Length == 0)
        {
            Message = Description_required_string;
            return;
        }

        try
        {
            var outcome = await PasskeyCreation.CreateAsync(PasskeyService, JavaScript, DialogService, description);
            switch (outcome)
            {
                case PasskeyCreationOutcome.Created:
                    NewPasskeyName = string.Empty;
                    await LoadSafe();
                    break;
                case PasskeyCreationOutcome.Unsupported:
                    Message = Browser_cannot_use_passkeys_string;
                    break;
                case PasskeyCreationOutcome.Cancelled:
                    Message = Passkey_not_added_string;
                    break;
                case PasskeyCreationOutcome.NotSignedIn:
                    NotSignedIn = true;
                    break;
            }
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

    protected void GoToLogin()
    {
        Navigator.NavigateTo(AuthenticationContents.AUTHENTICATION);
    }

    protected async Task RenameSafe(string id)
    {
        Message = null;
        var description = NewName.Trim();
        if (description.Length == 0)
        {
            Message = Description_required_string;
            return;
        }

        try
        {
            await PasskeyService.RenameAsync(id, description);
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
            var parameters = new DialogParameters<NConfirmationDialog>
            {
                { x => x.Description, Remove_passkey_confirmation_string },
                { x => x.ConfirmText, Remove_string },
            };
            var dialog = await DialogService.ShowAsync<NConfirmationDialog>(Remove_string, parameters);
            if (await dialog.IsCanceled())
            {
                return;
            }

            await PasskeyService.RemoveAsync(id);
            await LoadSafe();
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
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            NotSignedIn = true;
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}

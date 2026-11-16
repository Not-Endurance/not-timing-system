using Microsoft.JSInterop;
using MudBlazor;
using NC.Blazor.Dialogs.Abstractions;
using NoTiming.Ui.Contracts.Features.Account;

namespace NoTiming.Ui.Blazor.Features.Authentication;

public class PasskeyPromptDialogBehind : NDialog
{
    [Inject]
    IUiPasskeyService Passkeys { get; set; } = default!;

    [Inject]
    IJSRuntime JavaScript { get; set; } = default!;

    [Inject]
    IDialogService DialogService { get; set; } = default!;

    protected string Description { get; set; } = string.Empty;

    protected string? Message { get; private set; }

    protected async Task CreateSafe()
    {
        Message = null;
        var description = Description.Trim();
        if (description.Length == 0)
        {
            Message = Description_required_string;
            return;
        }

        try
        {
            var outcome = await PasskeyCreation.CreateAsync(Passkeys, JavaScript, DialogService, description);
            switch (outcome)
            {
                case PasskeyCreationOutcome.Created:
                    await ConfirmDialog();
                    break;
                case PasskeyCreationOutcome.Unsupported:
                    Message = Browser_cannot_use_passkeys_string;
                    break;
                case PasskeyCreationOutcome.Cancelled:
                    Message = Passkey_not_added_string;
                    break;
                case PasskeyCreationOutcome.NotSignedIn:
                    await CancelDialog();
                    break;
                case PasskeyCreationOutcome.Declined:
                    break;
            }
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}

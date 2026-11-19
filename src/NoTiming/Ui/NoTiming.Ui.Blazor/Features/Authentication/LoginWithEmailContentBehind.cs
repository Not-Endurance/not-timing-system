using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using NC.Blazor.Client.Authentication;
using NC.Blazor.Client.Authentication.Services;
using NC.Blazor.Components.Abstractions;
using NoTiming.Ui.Contracts.Features.Account;

namespace NoTiming.Ui.Blazor.Features.Authentication;

public class LoginWithEmailContentBehind : NComponent
{
    [Inject]
    IUiAccountService Account { get; set; } = default!;

    [Inject]
    IUiPasskeyService Passkeys { get; set; } = default!;

    [Inject]
    IJSRuntime JavaScript { get; set; } = default!;

    [Inject]
    SessionAuthenticationStateProvider Session { get; set; } = default!;

    [Inject]
    NavigationManager Navigator { get; set; } = default!;

    [Inject]
    IDialogService DialogService { get; set; } = default!;

    protected string Email { get; set; } = string.Empty;

    protected string Code { get; set; } = string.Empty;

    protected bool CodeSent { get; private set; }

    protected string? Message { get; private set; }

    protected void GoBack()
    {
        Navigator.NavigateTo(AuthenticationContents.AUTHENTICATION);
    }

    protected async Task SendCodeSafe()
    {
        Message = null;
        try
        {
            await Account.RequestSignInCodeAsync(Email);
            CodeSent = true;
        }
        catch (Exception ex) when (AccountMessages.For(ex) is { } message)
        {
            Message = message;
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task VerifySafe()
    {
        Message = null;
        try
        {
            await Account.VerifyCodeAsync(Email, Code);
            Session.Refresh();
            await OfferPasskeyAsync();
            Navigator.NavigateTo("/");
        }
        catch (Exception ex) when (AccountMessages.For(ex) is { } message)
        {
            Message = message;
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    async Task OfferPasskeyAsync()
    {
        try
        {
            await PasskeyPrompt.OfferIfNoneAsync(Passkeys, JavaScript, DialogService);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}

using Microsoft.AspNetCore.Components;
using NC.Blazor.Client.Authentication.Services;
using NC.Blazor.Components.Abstractions;
using NoTiming.Application.Contracts.Account;
using NoTiming.Ui.Contracts.Features.Account;

namespace NoTiming.Ui.Blazor.Features.Authentication;

public class RegisterContentBehind : NComponent
{
    [Inject]
    IUiAccountService Account { get; set; } = default!;

    [Inject]
    SessionAuthenticationStateProvider Session { get; set; } = default!;

    [Inject]
    NavigationManager Navigator { get; set; } = default!;

    protected string Email { get; set; } = string.Empty;

    protected string GivenName { get; set; } = string.Empty;

    protected string Surname { get; set; } = string.Empty;

    protected string CountryIsoCode { get; set; } = string.Empty;

    protected string Website { get; set; } = string.Empty;

    protected string Code { get; set; } = string.Empty;

    protected bool CodeSent { get; private set; }

    protected string? Message { get; private set; }

    protected async Task RegisterSafe()
    {
        Message = null;
        try
        {
            await Account.RequestRegistrationCodeAsync(
                new RegistrationRequest
                {
                    Email = Email,
                    GivenName = GivenName,
                    Surname = Surname,
                    Profile = new NoTimingProfile { CountryIsoCode = CountryIsoCode },
                    Website = Website,
                }
            );
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
}

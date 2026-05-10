using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Not.Application.Authentication.Abstractions;
using Not.Application.Authentication.User;
using Not.Blazor.Client.Authentication.Services;
using Not.Blazor.Components.Abstractions;

namespace Not.Blazor.Client.Authentication.Components;

public abstract class AuthenticateContentBehind : NComponent
{
    bool _hasAttemptedSilentSignin;

    [Inject]
    INAuthentication Authentication { get; set; } = default!;

    [Inject]
    INAuthenticationSession AuthenticationSession { get; set; } = default!;

    [Inject]
    INPendingUserRegistrationProfileStore PendingRegistrationProfiles { get; set; } = default!;

    protected bool IsRegistering { get; private set; }
    protected RegistrationProfileFormModel RegistrationProfile { get; } = new();

    protected async Task Signin()
    {
        await Authentication.Signin();
    }

    protected void ShowRegistration()
    {
        IsRegistering = true;
    }

    protected async Task Register()
    {
        await PendingRegistrationProfiles.Write(RegistrationProfile.ToProfile());
        await Authentication.Signin(preservePendingRegistrationProfile: true);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _hasAttemptedSilentSignin)
        {
            return;
        }

        _hasAttemptedSilentSignin = true;
        if (await AuthenticationSession.ShouldTryAutoSignin())
        {
            var hasPendingRegistrationProfile = await PendingRegistrationProfiles.Read() != null;
            await Authentication.Signin(
                silent: true,
                preservePendingRegistrationProfile: hasPendingRegistrationProfile
            );
        }
    }

    protected sealed class RegistrationProfileFormModel
    {
        [Required(ErrorMessage = "First name is required")]
        [StringLength(128)]
        public string? FirstName { get; set; }

        [StringLength(128)]
        public string? MiddleName { get; set; }

        [Required(ErrorMessage = "Last name is required")]
        [StringLength(128)]
        public string? LastName { get; set; }

        [StringLength(128)]
        public string? Club { get; set; }

        [StringLength(64)]
        public string? FeiId { get; set; }

        internal NUserRegistrationProfile ToProfile()
        {
            var firstName = Normalize(FirstName);
            var middleName = Normalize(MiddleName);
            var lastName = Normalize(LastName);
            var name = string.Join(
                " ",
                new[] { firstName, middleName, lastName }.Where(x => !string.IsNullOrWhiteSpace(x))
            );
            return new NUserRegistrationProfile(
                name,
                firstName,
                middleName,
                lastName,
                Normalize(Club),
                Normalize(FeiId)
            );
        }

        static string? Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}

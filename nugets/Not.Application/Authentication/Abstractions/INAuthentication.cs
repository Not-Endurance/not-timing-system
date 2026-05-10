namespace Not.Application.Authentication.Abstractions;

public interface INAuthentication
{
    Task Signin(bool silent = false, bool preservePendingRegistrationProfile = false);
    Task Signout();
}

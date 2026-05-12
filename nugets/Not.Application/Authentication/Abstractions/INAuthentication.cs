namespace Not.Application.Authentication.Abstractions;

public interface INAuthentication
{
    Task Signin(bool silent = false);
    Task Signout();
}

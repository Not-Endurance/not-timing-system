namespace Not.Application.Authentication.Abstractions;

public interface INAuthentication
{
    void Signin(bool silent = false);
    void Signout();
}

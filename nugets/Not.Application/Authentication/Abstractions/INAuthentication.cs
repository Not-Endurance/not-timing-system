namespace Not.Application.Authentication.Abstractions;

public interface INAuthentication
{
    void Signin();
    void Register();
    void Signout();
}

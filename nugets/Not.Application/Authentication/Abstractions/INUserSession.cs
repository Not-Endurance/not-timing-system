using Not.Injection;

namespace Not.Application.Authentication.Abstractions;

public interface INUserSession : IScoped
{
    Task Initialize();
}

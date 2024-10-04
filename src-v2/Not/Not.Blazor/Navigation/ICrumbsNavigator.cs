using Not.Injection;

namespace Not.Blazor.Navigation;

public interface ICrumbsNavigator : ITransientService
{
    void NavigateTo(string endpoint);
    void NavigateTo<T>(string endpoint, T parameter);
    void NavigateBack();
    T ConsumeParameter<T>();
}


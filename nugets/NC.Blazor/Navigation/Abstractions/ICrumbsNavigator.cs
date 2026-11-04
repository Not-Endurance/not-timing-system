using NC.Injection;

namespace NC.Blazor.Navigation.Abstractions;

public interface ICrumbsNavigator
{
    string CurrentEndpoint { get; }
    void NavigateTo(string endpoint);
    void NavigateTo<T>(string endpoint, T parameter);
    bool CanNavigateBack();
    void NavigateBack();
    T ConsumeParameter<T>();
}

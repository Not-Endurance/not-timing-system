using Not.Injection;

namespace Not.Blazor.Navigation;

/// <summary>
/// Landing servers as a start for Crumbs navigation. I.e. we need to Land and set a landing page
/// in order to be able to call NavigateBack and use that landing endpoint to go back to
/// </summary>
public interface ILandNavigator
{
    void LandTo(string endpoint);
    void Initialize(string landingEndpoint);
}

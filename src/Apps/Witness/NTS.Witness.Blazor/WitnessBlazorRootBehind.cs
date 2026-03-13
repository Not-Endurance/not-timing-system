using System.Reflection;
using Microsoft.AspNetCore.Components.Web;
using Not.Application.Authentication.Abstractions;
using Not.Blazor.Client.Authentication;
using Not.Blazor.Components.Abstractions;

namespace NTS.Witness.Blazor;

public class WitnessBlazorRootBehind : NComponent
{
    [Inject]
    INUserSession? UserSession { get; set; } = default!;

    protected IEnumerable<Assembly> RouteAssemblies { get; } =
        [typeof(WitnessBlazorRootBehind).Assembly, typeof(AuthenticationContents).Assembly];

    protected ErrorBoundary ErrorBoundary { get; set; } = default!;
    protected bool IsSessionInitialized { get; set; }

    [Parameter, EditorRequired]
    public Assembly Assembly { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            if (UserSession != null)
            {
                await UserSession.Initialize();
            }
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
        finally
        {
            IsSessionInitialized = true;
        }
    }
}

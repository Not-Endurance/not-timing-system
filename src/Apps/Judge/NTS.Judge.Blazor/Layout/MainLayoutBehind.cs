using Not.Blazor.Components.Layout;
using Not.Safe;
using Not.Startup;

namespace NTS.Judge.Blazor.Layout;

public class MainLayoutBehind : NLayoutBehind
{
    [Inject]
    IEnumerable<IStartupInitializerAsync> AsyncInitializers { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            foreach (var initializer in AsyncInitializers)
            {
                await initializer.RunAtStartupAsync();
            }
        }
        catch (Exception ex)
        {
            SafeHelper.HandleException(ex);
        }
    }
}

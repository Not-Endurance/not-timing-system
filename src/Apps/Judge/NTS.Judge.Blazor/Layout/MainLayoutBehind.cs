using Not.Blazor.Components.Layout;
using Not.Startup;

namespace NTS.Judge.Blazor.Layout;

public class MainLayoutBehind : NLayoutBehind
{
    [Inject]
    IEnumerable<IStartupInitializerAsync> AsyncInitializers { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        foreach (var initializer in AsyncInitializers)
        {
            await initializer.RunAtStartupAsync();
        }
    }
}

using MudBlazor;

namespace Not.Blazor.Components;

public class NotButtonPrint : NButtonSecondary
{
    public NotButtonPrint()
    {
        StartIcon = Icons.Material.Outlined.Print;
        Text = Print_string;
    }
}

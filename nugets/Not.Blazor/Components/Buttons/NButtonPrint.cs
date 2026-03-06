using MudBlazor;

namespace Not.Blazor.Components.Buttons;

public class NotButtonPrint : NButtonSecondary
{
    public NotButtonPrint()
    {
        StartIcon = Icons.Material.Outlined.Print;
        Text = Print_string;
    }
}

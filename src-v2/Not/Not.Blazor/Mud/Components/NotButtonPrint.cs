using MudBlazor;

namespace Not.Blazor.Mud.Components;

public class NotButtonPrint : NotButtonSecondary
{
    public NotButtonPrint()
    {
        StartIcon = Icons.Material.Outlined.Print;
        Text = "Print";
    }
}

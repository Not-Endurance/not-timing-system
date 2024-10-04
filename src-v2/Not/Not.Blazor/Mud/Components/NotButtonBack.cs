using MudBlazor;

namespace Not.Blazor.Mud.Components;

public class NotButtonBack : NotButtonSecondary
{
    public NotButtonBack()
    {
        StartIcon = Icons.Material.Outlined.ArrowBackIos;
        Text = "Back";
    }
}

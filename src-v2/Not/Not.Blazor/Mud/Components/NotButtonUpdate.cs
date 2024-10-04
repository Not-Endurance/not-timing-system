using MudBlazor;

namespace Not.Blazor.Mud.Components;

public class NotButtonUpdate : NotButtonPrimary
{
    public NotButtonUpdate()
    {
        StartIcon = Icons.Material.Filled.Create;
        Text = "Update";
    }
}
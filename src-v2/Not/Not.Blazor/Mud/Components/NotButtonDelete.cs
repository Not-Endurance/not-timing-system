using MudBlazor;

namespace Not.Blazor.Mud.Components;

public class NotButtonDelete : NotButtonPrimary
{
    public NotButtonDelete()
    {
        StartIcon = Icons.Material.Filled.Delete;
        Text = "Delete";
    }
}
using MudBlazor;

namespace Not.Blazor.Mud.Components;

public class NotButtonCreate : NotButtonPrimary
{
    public NotButtonCreate()
    {
        StartIcon = Icons.Material.Filled.Add;
        Text = "Create";
    }
}
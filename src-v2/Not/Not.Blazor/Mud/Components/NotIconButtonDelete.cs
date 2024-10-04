using MudBlazor;

namespace Not.Blazor.Mud.Components;

public class NotIconButtonDelete : MudIconButton
{
    public NotIconButtonDelete()
    {
        Icon = Icons.Material.Outlined.Delete;
        Color = Color.Error;
        Variant = Variant.Text;
        Size = Size.Medium;
    }
}

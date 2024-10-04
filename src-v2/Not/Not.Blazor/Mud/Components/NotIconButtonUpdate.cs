using MudBlazor;

namespace Not.Blazor.Mud.Components;

public class NotIconButtonUpdate : MudIconButton
{
    public NotIconButtonUpdate()
    {
        Icon = Icons.Material.Outlined.Create;
        Color = Color.Primary;
        Variant = Variant.Text;
        Size = Size.Medium;
    }
}

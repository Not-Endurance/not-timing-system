using MudBlazor;

namespace Not.Blazor.Components.Buttons;

public class NIconDelete : MudIconButton
{
    public NIconDelete()
    {
        Icon = Icons.Material.Outlined.Delete;
        Color = Color.Error;
        Variant = Variant.Text;
        Size = Size.Medium;
    }
}

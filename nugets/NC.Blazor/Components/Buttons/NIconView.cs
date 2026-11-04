using MudBlazor;

namespace Not.Blazor.Components.Buttons;

public class NIconView : MudIconButton
{
    public NIconView()
    {
        Icon = Icons.Material.Outlined.Visibility;
        Color = Color.Primary;
        Variant = Variant.Text;
        Size = Size.Medium;
    }
}

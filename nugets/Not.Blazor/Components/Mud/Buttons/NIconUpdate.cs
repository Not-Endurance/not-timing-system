using MudBlazor;

namespace Not.Blazor.Components;

public class NIconUpdate : MudIconButton
{
    public NIconUpdate()
    {
        Icon = Icons.Material.Outlined.Create;
        Color = Color.Primary;
        Variant = Variant.Text;
        Size = Size.Medium;
    }
}

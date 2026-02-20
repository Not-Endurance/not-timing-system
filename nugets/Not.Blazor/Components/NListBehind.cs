using Not.Blazor.Components.Abstractions;

namespace Not.Blazor.Components;

public class NListBehind<T> : NComponent
{
    [Parameter]
    public IReadOnlyList<T> Items { get; set; } = [];

    [Parameter]
    public bool IsLoading { get; set; }

    [Parameter]
    public Func<Task>? Create { get; set; }

    [Parameter]
    public Func<T, Task>? Update { get; set; }

    [Parameter]
    public Func<T, Task>? Delete { get; set; }

    [Parameter]
    public string? Title { get; set; }

    protected async Task OnCreate()
    {
        try
        {
            GuardHelper.ThrowIfDefault(Create);
            await Create();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task OnUpdate(T item)
    {
        try
        {
            GuardHelper.ThrowIfDefault(Update);
            await Update(item);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task OnDelete(T item)
    {
        try
        {
            GuardHelper.ThrowIfDefault(Delete);
            await Delete(item);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}

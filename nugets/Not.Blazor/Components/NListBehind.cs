using Not.Blazor.Components.Abstractions;

namespace Not.Blazor.Components;

public class NListBehind<T> : NComponent
{
    [Parameter]
    public IReadOnlyList<T> Items { get; set; } = [];

    [Parameter]
    public bool IsLoading { get; set; }

    [Parameter]
    public Func<Task>? CreateSafe { get; set; }

    [Parameter]
    public Func<T, Task>? UpdateSafe { get; set; }

    [Parameter]
    public Func<T, Task>? DeleteSafe { get; set; }

    [Parameter]
    public string? Title { get; set; }

    protected async Task OnCreate()
    {
        try
        {
            GuardHelper.ThrowIfDefault(CreateSafe);
            await CreateSafe();
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
            GuardHelper.ThrowIfDefault(UpdateSafe);
            await UpdateSafe(item);
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
            GuardHelper.ThrowIfDefault(DeleteSafe);
            await DeleteSafe(item);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }
}

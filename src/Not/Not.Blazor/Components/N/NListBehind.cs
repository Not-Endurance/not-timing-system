// ReSharper disable once CheckNamespace
namespace Not.Blazor.Components;

public class NListBehind<T> : NComponent
{
    [Parameter, EditorRequired]
    public IEnumerable<T> Items { get; set; } = [];
    
    [Parameter]
    public string? Title { get; set; }
    
    [Parameter]
    public Func<Task>? CreateHandler { get; set; }
    
    [Parameter]
    public Func<T, Task>? UpdateHandler { get; set; }
    
    [Parameter]
    public Func<T, Task>? DeleteHandler { get; set; }
    
    [Parameter]
    public bool IsLoading { get; set; }
}

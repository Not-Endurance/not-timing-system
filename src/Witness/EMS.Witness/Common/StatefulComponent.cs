using Core.Events;
using Microsoft.AspNetCore.Components;

namespace EMS.Witness.Common;

public abstract class StatefulComponent : ComponentBase, IDisposable
{
    public StatefulComponent()
    {
        AppState.Changed += OnStateChanged;
    }

	public virtual void Dispose()
	{
        AppState.Changed -= OnStateChanged;
	}

	/// <summary>
	/// Compares the references of component's state and changedState param and calls StateHasChanged if equal
	/// </summary>
	/// <param name="sender"></param>
	/// <param name="changedState"></param>
	/// <returns></returns>
	protected virtual async void OnStateChanged(object? sender, object changedState)
    {
        if (this.ShouldRender())
        {
            await this.InvokeAsync(this.StateHasChanged);
        }
    }

    protected abstract bool ShouldRender(object changedState);
}

using Not.Blazor.Ports;
using Not.Events;
using Not.Observables;
using Not.Safe;

namespace Not.Application.Behinds.Adapters;

// TODO: Implement IObservable; Create and Implement IObserver in NBehind since it already inherits ComponentBase
public abstract class NStatefulService : Observer, IStatefulService
{
    readonly SemaphoreSlim _semaphore = new(1);
    readonly Event _changed = new();
    bool _isInitialized;

    public IEventSubscriber Event => _changed;

    /// <summary>
    /// Creates the service state. Runs on initial render as long as <seealso cref="Blazor.Components.NComponent.Observe(IStatefulService)"/> is used.
    /// Guaranteed to run only once unless <seealso cref="ResetState" /> is called
    /// </summary>
    /// <returns>Indicates weather or not the state has been initialized successfully</returns>
    protected abstract Task<bool> CreateState(params IEnumerable<object> arguments);

    protected void EmitChanged()
    {
        _changed.Emit();
    }

    /// <summary>
    /// Resets the service state, which will cause <seealso cref="CreateState(IEnumerable{object})"/> to execute again on next Render.
    /// </summary>
    public void ResetState()
    {
        _isInitialized = false;
    }

    public async Task Initialize(params IEnumerable<object> arguments)
    {
        try
        {
            await _semaphore.WaitAsync();
            if (_isInitialized)
            {
                return;
            }
            _isInitialized = await SafeHelper.Run(() => CreateState(arguments));
        }
        finally
        {
            _semaphore.Release();
        }
    }
}

using Not.Blazor.Ports.Behinds;
using Not.Events;
using Not.Safe;

namespace Not.Application.Adapters.Behinds;

// TODO: Probably rename to EventBehind. However we need to rename the Domain entity Event
// To EnduranceEvent to avoid name conflicts
public abstract class ObservableBehind : IObservableBehind
{
    readonly SemaphoreSlim _semaphore = new(1);
    bool _isInitialized;
    private readonly Event _stateChanged = new();

    /// <summary>
    /// Initialize the state of an ObservableBehind. 
    /// If the state has been initialized successfully It cannot be initialized again.
    /// </summary>
    /// <returns>Indicates weather or not the state has been initialized successfully</returns>
    protected abstract Task<bool> PerformInitialization(params IEnumerable<object> arguments);

    public async Task Initialize(params IEnumerable<object> arguments)
    {
        if (_isInitialized)
        {
            return;
        }

        try
        {
            await _semaphore.WaitAsync();
            _isInitialized = await SafeHelper.Run(() => PerformInitialization(arguments));
        }
        finally
        {
            _semaphore.Release();
        }
    }
    
    public void Subscribe(Func<Task> action)
    {
        _stateChanged.SubscribeAsync(action);
    }

    protected void EmitChange()
    {
        _stateChanged.Emit();
    }
}

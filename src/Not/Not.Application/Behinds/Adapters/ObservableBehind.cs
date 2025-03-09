using Not.Blazor.Ports;
using Not.Events;
using Not.Safe;

namespace Not.Application.Behinds.Adapters;

// TODO: Probably rename to EventBehind. However we need to rename the Domain entity Event
// To EnduranceEvent to avoid name conflicts
public abstract class ObservableBehind : IObservableBehind
{
    readonly SemaphoreSlim _semaphore = new(1);
    readonly Event _stateChanged = new();
    bool _isInitialized;

    /// <summary>
    /// Initialize the state of an ObservableBehind.
    /// If the state has been initialized successfully It cannot be initialized again.
    /// </summary>
    /// <returns>Indicates weather or not the state has been initialized successfully</returns>
    protected abstract Task<bool> PerformInitialization(params IEnumerable<object> arguments);

    protected bool Initialized { get; } = false;

    protected void EmitChange()
    {
        _stateChanged.Emit();
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
}

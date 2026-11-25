using NoTiming.EventBus.Abstractions;
using NoTiming.EventBus.Contracts.Features.Ui.Procedures;

namespace NoTiming.EventBus.Features.Witness;

/// <summary>
/// Witness (Ui) clients join this hub to receive change notices. They send nothing through it: writes go through the API.
/// </summary>
internal class WitnessRpcHub : NoTimingHubBase<IUiClientProcedures>
{
    public WitnessRpcHub(ILogger<WitnessRpcHub> logger)
        : base(logger) { }
}

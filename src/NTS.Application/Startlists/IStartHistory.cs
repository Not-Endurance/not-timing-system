using Not.Application.Behinds.Adapters;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Application.Startlists;

public interface IStartHistory : IStatefulService
{
    IReadOnlyList<StartlistEntry> History { get; }
}

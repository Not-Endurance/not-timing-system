using Not.Collections;
using NTS.Domain.Core.Objects.Startlists;

namespace NTS.Warp.Features.Witness.Procedures;

public interface IStartlistClientProcedures
{
    Task ReceiveStartlistEntry(StartlistEntry entry, NCollectionAction action);
}

using Core.Domain.AggregateRoots.Manager.Aggregates.Startlists;
using Core.Enums;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Core.Application.Rpc.Procedures;

public interface IStartlistClientProcedures
{
    Task ReceiveEntry(StartlistEntry entry, CollectionAction action);
}

public interface IStartlistHubProcedures
{
    Dictionary<int, Startlist> SendStartlist();
}

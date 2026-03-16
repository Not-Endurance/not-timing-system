using NTS.Domain.Core.Aggregates;

namespace NTS.Application.Socket;

public interface ISocketPrincipalStorage
{
    Task<EnduranceEvent?> Get();
    Task Commit(EnduranceEvent? enduranceEvent);
}

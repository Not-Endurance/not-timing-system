using NTS.Relay.ACL.Abstractions;

namespace NTS.Relay.ACL.Entities.Results;

public interface IEmsResultState : IEmsIdentifiable
{
    bool IsNotQualified { get; }

    string Code { get; }
}

using NTS.Warp.ACL.Abstractions;

namespace NTS.Warp.ACL.Entities.Results;

public interface IEmsResultState : IEmsIdentifiable
{
    bool IsNotQualified { get; }

    string Code { get; }
}

using Not.Injection;

namespace NTS.Judge.Blazor.Core.Rankings.Protocols;

public interface ILogoPersistence: ISingleton
{
    string Left { get; set; }
    string Right { get; set; }
}

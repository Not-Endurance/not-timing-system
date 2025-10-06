using Not.Injection;

namespace NTS.Judge.Blazor.Core.Rankings.Protocols;

public interface IProtocolLogoPersistence : ISingleton
{
    string DirPath { get; }
    string Left { get; set; }
    string Right { get; set; }
    void SetLogo(string newLogo, string oldLogo);
}

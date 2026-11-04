using Not.Injection;

namespace NTS.Judge.Blazor.Features.Core.Rankings.Protocols;

public interface IProtocolLogoState : ISingleton
{
    string DirPath { get; }
    string Left { get; set; }
    string Right { get; set; }
    void SetLogo(string newLogo, string oldLogo);
}

using NTS.Warp.ACL.Abstractions;

namespace NTS.Warp.ACL.Entities.Countries;

public interface IEmsCountryState : IEmsIdentifiable
{
    string IsoCode { get; }

    string Name { get; }
}

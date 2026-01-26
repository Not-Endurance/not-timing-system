using Not.Domain;
using NTS.Domain.Enums;
using NTS.Domain.Settings;

namespace NTS.Domain.Aggregates;

public class Setting : Aggregate
{
    public Setting(Guid? accountId, Country? country, DetectionMode? detectionMode, int? id = null)
        : base(id)
    {
        AccountId = Required(nameof(AccountId), accountId);
        Country = Required(nameof(Country), country);
        DetectionMode = detectionMode;
        StaticSettings.Instance = this;
    }

    public Guid AccountId { get; }
    public Country Country { get; }
    public DetectionMode? DetectionMode { get; }

    public override string ToString()
    {
        return Combine(AccountId, Country);
    }
}

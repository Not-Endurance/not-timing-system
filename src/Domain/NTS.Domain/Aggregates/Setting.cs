using Not.Domain;
using NTS.Domain.Enums;
using NTS.Domain.Settings;

namespace NTS.Domain.Aggregates;

public class Setting : Aggregate
{
    public Setting(Country? country, DetectionMode? detectionMode, int? id = null)
        : base(id)
    {
        Country = Required(nameof(Country), country);
        DetectionMode = detectionMode;
        StaticSettings.Instance = this;
    }

    // TODO: Keep Id on Profile level, decouple settings
    public Guid AccountId { get; } = Guid.Parse("ec6d8f0d-ecad-4fb6-a10f-fdb190dc0cd4");
    public Country Country { get; }
    public DetectionMode? DetectionMode { get; }
    // TODO: Add Language here

    public override string ToString()
    {
        return Combine(AccountId, Country);
    }
}

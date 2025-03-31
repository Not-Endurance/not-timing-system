using Not.Domain.Base;
using NTS.Domain.Aggregates;
using NTS.Domain.Enums;

namespace NTS.Domain.Settings;

public class Setting : AggregateRoot
{
    public Setting(Guid accountId, Country? country, DetectionMode? detectionMode)
        : this(GenerateId(), accountId, country, detectionMode) { }

    [Newtonsoft.Json.JsonConstructor]
    [System.Text.Json.Serialization.JsonConstructor]
    public Setting(int? id, Guid? accountId, Country? country, DetectionMode? detectionMode)
        : base(id!.Value)
    {
        AccountId = Required(nameof(AccountId), accountId);
        Country = Required(nameof(Country), country);
        DetectionMode = Required(nameof(DetectionMode), detectionMode);
        StaticSettings.Instance = this;
    }

    public Guid AccountId { get; }
    public Country Country { get; }
    public DetectionMode DetectionMode { get; }

    public override string ToString()
    {
        return Combine(AccountId, Country);
    }
}

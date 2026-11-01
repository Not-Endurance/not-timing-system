using System.Globalization;
using System.Linq.Expressions;
using Not.Application.HTTP;
using NTS.Domain.Core.Aggregates;

namespace NTS.Tests.Unit.Harness;

public sealed class Sample
{
    public Guid EventId { get; set; }
    public Guid? UserId { get; set; }
    public bool IsNotRanked { get; set; }
    public int Number { get; set; }
    public double Speed { get; set; }
    public string? Name { get; set; }
    public string? NameEnglish { get; set; }
    public SampleKind Kind { get; set; }
    public Sample? Country { get; set; }
}

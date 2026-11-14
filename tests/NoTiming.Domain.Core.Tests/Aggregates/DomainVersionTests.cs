using NC.Domain.Exceptions;
using NoTiming.Domain.Core.Aggregates.Participations.Entities;
using NoTiming.Domain.Objects;

namespace NoTiming.Domain.Core.Tests.Aggregates;

public sealed class DomainVersionTests
{
    [Fact]
    public void Constructor_WhenStoredVersionMatchesCode_ShouldLoad()
    {
        var phase = CreatePhase(storedDomainVersion: 1);

        Assert.Equal(1, phase.DomainVersion);
    }

    [Fact]
    public void Constructor_WhenStoredVersionIsMissing_ShouldRefuseIt()
    {
        var exception = Assert.Throws<DomainException>(() => CreatePhase(storedDomainVersion: 0));

        Assert.Equal("Phase: stored 0, code 1, delta -1", exception.Message);
    }

    [Fact]
    public void Constructor_WhenStoredVersionIsNewer_ShouldRefuseIt()
    {
        var exception = Assert.Throws<DomainException>(() => CreatePhase(storedDomainVersion: 2));

        Assert.Equal("Phase: stored 2, code 1, delta 1", exception.Message);
    }

    [Fact]
    public void Constructor_WhenNoStoredVersionIsGiven_ShouldBuildNewEntity()
    {
        var phase = CreatePhase(storedDomainVersion: null);

        Assert.Equal(1, phase.DomainVersion);
    }

    static Phase CreatePhase(int? storedDomainVersion)
    {
        return new Phase(
            "GATE1",
            20,
            40,
            40,
            false,
            null,
            null,
            null,
            null,
            null,
            false,
            false,
            false,
            id: 1,
            storedDomainVersion: storedDomainVersion
        );
    }
}

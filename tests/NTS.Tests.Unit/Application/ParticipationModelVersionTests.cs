using Not.Krud.Abstractions;
using NTS.Contracts.Core.Models;
using NTS.Contracts.Shared;
using NTS.Domain.Core.Aggregates;

namespace NTS.Tests.Unit.Application;

/// <summary>
/// The version a Participation counts its writes by (ADR-0013) goes where the Participation goes: from the document into
/// the Participation that is made of it, and from a Participation into the document that is made of it, by each of the ways
/// a model is made. A change is sent against the version that was read, so one that is lost on the way is a change that
/// the Api takes for stale, or, worse, for current.
/// </summary>
public sealed class ParticipationModelVersionTests
{
    [Fact]
    public void The_version_of_a_document_is_the_version_of_the_Participation_made_of_it()
    {
        var model = ParticipationModel.MapFrom(ParticipationFixtures.Active(1));
        model.Version = 5;

        var participation = model.MapToEntity();

        Assert.Equal(5, participation.Version);
    }

    [Fact]
    public void The_version_of_a_Participation_is_the_version_of_the_document_made_of_it_by_either_mapping()
    {
        var participation = Versioned(5);

        var byTheStaticMapping = ParticipationModel.MapFrom(participation);
        var byTheModelsOwn = new ParticipationModel();
        ((IKrudModel<Participation>)byTheModelsOwn).MapFrom(participation);

        Assert.Equal(5, byTheStaticMapping.Version);
        Assert.Equal(5, byTheModelsOwn.Version);
    }

    [Fact]
    public void A_document_that_was_never_written_since_the_versions_came_is_at_version_0()
    {
        var participation = ParticipationFixtures.Active(1);

        Assert.Equal(0, participation.Version);
        Assert.Equal(0, ParticipationModel.MapFrom(participation).Version);
        Assert.IsAssignableFrom<IVersionedDocument>(ParticipationModel.MapFrom(participation));
    }

    static Participation Versioned(int version)
    {
        var model = ParticipationModel.MapFrom(ParticipationFixtures.Active(1));
        model.Version = version;
        return model.MapToEntity();
    }
}

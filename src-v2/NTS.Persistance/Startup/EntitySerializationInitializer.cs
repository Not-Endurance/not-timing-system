using Not.Injection;
using Not.Serialization;
using Not.Startup;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Setup.Entities;
using NTS.Persistence.Converters;

namespace NTS.Persistence.Startup;

public class EntitySerializationInitializer : IStartupInitializer, ITransientService
{
    public void RunAtStartup()
    {
        // TODO: this should happen automatically for all entities probably, since there is no relevant performance hit or drawback I'm aware of
        // Probably pair with structuring the state objects in a way that can't mess up the entity order
        SerializationExtensions.AddConverter(new EntityReferenceEqualityGuardConverter<Horse>());
        SerializationExtensions.AddConverter(new EntityReferenceEqualityGuardConverter<Athlete>());
        SerializationExtensions.AddConverter(new EntityReferenceEqualityGuardConverter<Combination>());
        SerializationExtensions.AddConverter(new EntityReferenceEqualityGuardConverter<Loop>());
        SerializationExtensions.AddConverter(new EntityReferenceEqualityGuardConverter<Domain.Setup.Entities.Competition>());
        SerializationExtensions.AddConverter(new EntityReferenceEqualityGuardConverter<Official>());
        SerializationExtensions.AddConverter(new EntityReferenceEqualityGuardConverter<Contestant>());
        SerializationExtensions.AddConverter(new EntityReferenceEqualityGuardConverter<Domain.Setup.Entities.Phase>());
        SerializationExtensions.AddConverter(new EntityReferenceEqualityGuardConverter<Participation>());
    }
}

using Not.Injection;
using Not.Serialization;
using Not.Startup;
using NTS.Persistence.Converters;

namespace NTS.Persistence.Startup;

public class EntitySerializationInitializer : IStartupInitializer, ITransientService
{
    public void RunAtStartup()
    {
        // TODO: this should happen automatically for all entities probably, since there is no relevant performance hit or drawback I'm aware of
        // Probably pair with structuring the state objects in a way that can't mess up the entity order
        SerializationExtensions.AddConverter(new EntityReferenceEqualityGuardConverter<Domain.Setup.Entities.Horse>());
        SerializationExtensions.AddConverter(new EntityReferenceEqualityGuardConverter<Domain.Setup.Entities.Athlete>());
        SerializationExtensions.AddConverter(new EntityReferenceEqualityGuardConverter<Domain.Setup.Entities.Combination>());
        SerializationExtensions.AddConverter(new EntityReferenceEqualityGuardConverter<Domain.Setup.Entities.Loop>());
        SerializationExtensions.AddConverter(new EntityReferenceEqualityGuardConverter<Domain.Setup.Entities.Competition>());
        SerializationExtensions.AddConverter(new EntityReferenceEqualityGuardConverter<Domain.Setup.Entities.Official>());
        SerializationExtensions.AddConverter(new EntityReferenceEqualityGuardConverter<Domain.Setup.Entities.Participation>());
        SerializationExtensions.AddConverter(new EntityReferenceEqualityGuardConverter<Domain.Setup.Entities.Phase>());
        SerializationExtensions.AddConverter(new EntityReferenceEqualityGuardConverter<Domain.Core.Entities.Participation>());
    }
}

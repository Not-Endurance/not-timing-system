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
        SerializationExtensions.AddConverter(new EntityReferenceEqualityGuardConverter<Horse>());
        SerializationExtensions.AddConverter(new EntityReferenceEqualityGuardConverter<Athlete>());
        SerializationExtensions.AddConverter(new EntityReferenceEqualityGuardConverter<Combination>());
        SerializationExtensions.AddConverter(new EntityReferenceEqualityGuardConverter<Loop>());
        SerializationExtensions.AddConverter(new EntityReferenceEqualityGuardConverter<Participation>());
    }
}

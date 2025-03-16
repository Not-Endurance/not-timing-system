using Not.Injection;
using Not.Serialization;
using Not.Startup;
using NTS.Storage.JSON.Converters;

namespace NTS.Storage.JSON.Initializers;

public class EntityReferenceConverterInitializer : IStartupInitializer, ITransient
{
    public void RunAtStartup()
    {
        // TODO: this should happen automatically for all entities probably, since there is no relevant performance hit or drawback I'm aware of
        // Probably pair with structuring the state objects in a way that can't mess up the entity order
        SerializationExtensions.AddConverter(new EntityReferenceConverter<Domain.Setup.Aggregates.Horse>());
        SerializationExtensions.AddConverter(new EntityReferenceConverter<Domain.Setup.Aggregates.Athlete>());
        SerializationExtensions.AddConverter(new EntityReferenceConverter<Domain.Setup.Aggregates.Combination>());
        SerializationExtensions.AddConverter(new EntityReferenceConverter<Domain.Setup.Aggregates.Loop>());
        SerializationExtensions.AddConverter(new EntityReferenceConverter<Domain.Setup.Aggregates.Competition>());
        SerializationExtensions.AddConverter(new EntityReferenceConverter<Domain.Setup.Aggregates.Official>());
        SerializationExtensions.AddConverter(new EntityReferenceConverter<Domain.Setup.Aggregates.Participation>());
        SerializationExtensions.AddConverter(new EntityReferenceConverter<Domain.Setup.Aggregates.Phase>());
        SerializationExtensions.AddConverter(new EntityReferenceConverter<Domain.Core.Aggregates.Participation>());
    }
}

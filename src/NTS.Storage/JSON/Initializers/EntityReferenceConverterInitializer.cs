using Not.Injection;
using Not.Startup;
using Not.Storage.Stores.Files;
using NTS.Storage.JSON.Converters;

namespace NTS.Storage.JSON.Initializers;

public class EntityReferenceConverterInitializer : IStartupInitializer, ITransient
{
    public void RunAtStartup()
    {
        // TODO: this should happen automatically for all entities probably, since there is no relevant performance hit or drawback I'm aware of
        // Probably pair with structuring the state objects in a way that can't mess up the entity order
        // JsonFileStore.AddConverter(new EntityReferenceConverter<Domain.Setup.Aggregates.Horse>());
        // JsonFileStore.AddConverter(new EntityReferenceConverter<Domain.Setup.Aggregates.Athlete>());
        // JsonFileStore.AddConverter(new EntityReferenceConverter<Domain.Setup.Aggregates.Combination>());
        // JsonFileStore.AddConverter(new EntityReferenceConverter<Domain.Setup.Aggregates.Loop>());
        // JsonFileStore.AddConverter(new EntityReferenceConverter<Domain.Setup.Aggregates.Competition>());
        // JsonFileStore.AddConverter(new EntityReferenceConverter<Domain.Setup.Aggregates.Official>());
        // JsonFileStore.AddConverter(new EntityReferenceConverter<Domain.Setup.Aggregates.Participation>());
        // JsonFileStore.AddConverter(new EntityReferenceConverter<Domain.Setup.Aggregates.Phase>());
        JsonFileStore.AddConverter(new EntityReferenceConverter<Domain.Core.Aggregates.Participation>());
    }
}

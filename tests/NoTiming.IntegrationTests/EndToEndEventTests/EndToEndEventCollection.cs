using NoTiming.IntegrationTests.Infrastructure;

namespace NoTiming.IntegrationTests.EndToEndEventTests;

[CollectionDefinition(EndToEndEventCollection.Name, DisableParallelization = true)]
public sealed class EndToEndEventCollection : ICollectionFixture<NtsIntegrationFixture>
{
    public const string Name = "End-to-end event";
}

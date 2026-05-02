using NTS.Tests.Integration.Infrastructure;

namespace NTS.Tests.Integration.EndToEndEventTests;

[CollectionDefinition(EndToEndEventCollection.Name, DisableParallelization = true)]
public sealed class EndToEndEventCollection : ICollectionFixture<NtsIntegrationFixture>
{
    public const string Name = "End-to-end event";
}
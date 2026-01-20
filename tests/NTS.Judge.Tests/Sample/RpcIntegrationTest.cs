using NTS.Domain.Aggregates;
using NTS.Domain.Enums;
using NTS.Judge.Features.Core.Dashboard;
using NTS.Judge.Tests.Helpers;
using NTS.Storage.Core;
using Xunit.Abstractions;

namespace NTS.Judge.Tests.Sample;

[Collection(nameof(WitnessRpcFixture))]
public class RpcIntegrationTest : JudgeIntegrationTest
{
    readonly WitnessRpcFixture _witnessFIxture;
    readonly ITestOutputHelper _testOutputHelper;

    public RpcIntegrationTest(WitnessRpcFixture witnessFixture, ITestOutputHelper testOutputHelper)
        : base(nameof(CoreState), testOutputHelper)
    {
        _witnessFIxture = witnessFixture;
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task TestEliminatedOnRpcClient()
    {
        await Seed();

        var timestamp = TimestampHelper.Create(hour: 18, minute: 10);
        var snapshot = new Snapshot(1337, SnapshotType.Vet, SnapshotMethod.Manual, timestamp);

        var processor = await GetBehind<ISnapshotProcessor>(_testOutputHelper.WriteLine);

        await AssertRpcInvoked(
            _witnessFIxture,
            () => processor.Process(snapshot),
            nameof(WitnessTestClient.ReceiveEntryUpdate)
        );
    }
}

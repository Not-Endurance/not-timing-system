using Microsoft.Extensions.DependencyInjection;
using Not.Blazor.CRUD.Ports;
using Not.Serialization;
using NTS.Domain.Aggregates;
using NTS.Domain.Setup.Aggregates;
using NTS.Judge.Blazor.Setup.EnduranceEvents;
using NTS.Storage.Setup;
using Xunit.Abstractions;

namespace NTS.Judge.Tests.Sample;

public class StorageTests : JudgeIntegrationTest
{
    public StorageTests(ITestOutputHelper testOutputHelper)
        : base(nameof(SetupState), testOutputHelper) { }

    [Fact]
    public async Task Test1()
    {
        await Seed();

        var enduranceEventBehind = Provider.GetRequiredService<ICreateBehind<EnduranceEventFormModel>>();

        var country = new Country(0, "testIso", "testNf", "Test");
        var enduranceEvent = new EnduranceEventTestModel { Country = country, Place = "Sofia" };

        await enduranceEventBehind.Create(enduranceEvent);

        var expectedState = new SetupState
        {
            EnduranceEvent = EnduranceEvent.Update(
                enduranceEvent.Id,
                enduranceEvent.Place,
                enduranceEvent.Country,
                [],
                []
            ),
        };

        await AssertStateEquals(expectedState.ToConvertedJson());
    }

    [Fact]
    public async Task SeedResourceTest()
    {
        await Seed();

        var expected = """
            {
                "EnduranceEvent": {
                "Place": "Sofia",
                "Country": {
                    "IsoCode": "testIso",
                    "NfCode": "testNf",
                    "Name": "Test"
                },
                "Officials": [],
                "Competitions": [],
                "Id": 319178201
                }
            }
            """;

        await AssertStateEquals(expected);
    }
}

public class EnduranceEventTestModel : EnduranceEventFormModel
{
    public new int Id { get; set; }
}

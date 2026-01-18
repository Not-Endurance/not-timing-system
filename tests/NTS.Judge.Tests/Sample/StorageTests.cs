using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Not.Application.Services;
using Not.Serialization.JSON;
using NTS.Domain.Aggregates;
using NTS.Judge.Features.Setup.UpcomingEvents;
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

        var enduranceEventBehind = Provider.GetRequiredService<ICreateBehind<UpcomingEventFormModel>>();

        var country = new Country(0, "testIso", "testNf", "Test", "bg-BG");
        var enduranceEvent = new EnduranceEventTestModel { Country = country, Place = "Sofia" };

        await enduranceEventBehind.Create(enduranceEvent);

        var expectedState = new SetupState
        {
            // UpcomingEvent = UpcomingEvent.Update(
            //     enduranceEvent.Id,
            //     enduranceEvent.Name,
            //     enduranceEvent.Place,
            //     enduranceEvent.Country,
            //     enduranceEvent.FeiShowId,
            //     [],
            //     [],
            //     [],
            //     []
            // ),
        };
        var settings = new NJsonSettings();
        var expected = JsonConvert.SerializeObject(expectedState, settings);
        await AssertStateEquals(expected);
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

public class EnduranceEventTestModel : UpcomingEventFormModel
{
    public new int Id { get; set; }
}

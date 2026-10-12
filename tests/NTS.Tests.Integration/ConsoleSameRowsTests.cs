using NTS.Contracts.Features.Console.Setup;
using NTS.Tests.Integration.Infrastructure;
using static NTS.Tests.Integration.Infrastructure.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// The registry's guard against a second copy (#646, ADR-0010): the rows of the Tenant and of the others that an Athlete or a Horse
/// about to be registered is the same as, by its name in either script or by its FEI ID, as the Api tells of them. Over the real Api
/// in this process, with the services of the Ui built as the app builds them.
/// </summary>
public sealed class ConsoleSameRowsTests : IClassFixture<MongoFixture>
{
    readonly MongoFixture _mongo;

    public ConsoleSameRowsTests(MongoFixture mongo)
    {
        _mongo = mongo;
    }

    [Fact]
    public async Task An_Athlete_is_the_same_as_a_row_of_its_Tenant_by_its_name_in_either_script_or_by_its_FEI_ID()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var scene = await SceneAsync(api);
        var rows = scene.Ui.Get<IConsoleSameRows>();

        var byName = await rows.OfAthlete(new AthleteFormModel { Name = $"  мария   тенант {scene.Token}" });
        var byEnglish = await rows.OfAthlete(
            new AthleteFormModel { Name = "Нова", NameEnglish = $"MARIA TENANT {scene.Token}" }
        );
        var byFeiId = await rows.OfAthlete(new AthleteFormModel { Name = "Съвсем друга", FeiId = "0012" });
        var different = await rows.OfAthlete(new AthleteFormModel { Name = "Съвсем друга", FeiId = "13" });

        Assert.Equal([(scene.Own, SameRowReason.Name, true)], byName.Select(x => (x.Id, x.Reason, x.IsOwn)));
        Assert.Equal([(scene.Own, SameRowReason.Name, true)], byEnglish.Select(x => (x.Id, x.Reason, x.IsOwn)));
        Assert.Equal([(scene.Own, SameRowReason.FeiId, true)], byFeiId.Select(x => (x.Id, x.Reason, x.IsOwn)));
        Assert.Empty(different);
    }

    [Fact]
    public async Task An_Athlete_is_the_same_as_a_row_of_another_Tenant_that_the_search_finds_by_its_name()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var scene = await SceneAsync(api);
        var rows = scene.Ui.Get<IConsoleSameRows>();

        var found = await rows.OfAthlete(new AthleteFormModel { Name = $"Мария Друга {scene.Token}" });
        var byEnglish = await rows.OfAthlete(
            new AthleteFormModel { Name = "Нова", NameEnglish = $"maria other {scene.Token}" }
        );
        var tooShort = await rows.OfAthlete(new AthleteFormModel { Name = "Ма" });

        var row = Assert.Single(found);
        Assert.Equal((scene.Theirs, SameRowReason.Name, false), (row.Id, row.Reason, row.IsOwn));
        Assert.Equal([scene.Theirs], byEnglish.Select(x => x.Id));
        Assert.Empty(tooShort); // the other Tenants are not asked for less than three characters, and the Tenant has no row by it
    }

    [Fact]
    public async Task The_other_Tenants_are_asked_for_a_name_of_three_characters_or_more_and_once_for_each_registry_whatever_its_case()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var scene = await SceneAsync(api);
        var rows = scene.Ui.Get<IConsoleSameRows>();
        int Searches() => scene.Ui.Requests.Asked.Count(x => x.Contains("/all-tenants", StringComparison.Ordinal));

        await rows.OfAthlete(new AthleteFormModel { Name = "Ма" });
        var short2 = Searches();
        await rows.OfAthlete(new AthleteFormModel { Name = "Мар" });
        var first = Searches();
        await rows.OfAthlete(new AthleteFormModel { Name = "  МАР " });
        await rows.OfAthlete(new AthleteFormModel { Name = "мар", NameEnglish = "мар" });
        var again = Searches();
        await rows.OfHorse(new HorseFormModel { Name = "Мар" });
        var horses = Searches();

        Assert.Equal(0, short2); // two characters are not a name to ask the other Tenants for
        Assert.Equal(1, first);
        Assert.Equal(1, again); // a name is asked once, in whatever case and with whatever spaces around it
        Assert.Equal(2, horses); // and the Horses are another registry
        Assert.Contains(scene.Ui.Requests.Asked, x => x.Contains("/athletes/all-tenants", StringComparison.Ordinal));
        Assert.Contains(scene.Ui.Requests.Asked, x => x.Contains("/horses/all-tenants", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_row_that_is_renamed_is_not_the_same_as_itself()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var scene = await SceneAsync(api);
        var rows = scene.Ui.Get<IConsoleSameRows>();

        var renamed = await rows.OfAthlete(
            new AthleteFormModel
            {
                Id = scene.Own,
                Name = $"Мария Тенант {scene.Token}",
                FeiId = "12",
            }
        );

        Assert.Empty(renamed);
    }

    [Fact]
    public async Task A_Horse_is_the_same_as_a_row_by_its_name_or_by_its_FEI_ID_as_the_text_it_is()
    {
        await using var api = new ApiFactory(_mongo.ConnectionString);
        var scene = await SceneAsync(api);
        var rows = scene.Ui.Get<IConsoleSameRows>();

        var byName = await rows.OfHorse(new HorseFormModel { Name = $"далечен {scene.Token}" });
        var byFeiId = await rows.OfHorse(new HorseFormModel { Name = "Друг кон", FeiId = "xyz-9" });
        var notANumber = await rows.OfHorse(new HorseFormModel { Name = "Друг кон", FeiId = "9" });
        var numeric = await rows.OfHorse(new HorseFormModel { Name = "Друг кон", FeiId = "0012" });
        var notThatNumber = await rows.OfHorse(new HorseFormModel { Name = "Друг кон", FeiId = "12" });

        Assert.Equal([(scene.Horse, SameRowReason.Name, false)], byName.Select(x => (x.Id, x.Reason, x.IsOwn)));
        Assert.Equal([(scene.OwnHorse, SameRowReason.FeiId, true)], byFeiId.Select(x => (x.Id, x.Reason, x.IsOwn)));
        Assert.Empty(notANumber);
        Assert.Equal([(scene.NumericHorse, SameRowReason.FeiId, true)], numeric.Select(x => (x.Id, x.Reason, x.IsOwn)));
        Assert.Empty(notThatNumber); // the digits of a Horse's ID are text: 0012 is not 12
    }

    async Task<Scene> SceneAsync(ApiFactory api)
    {
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var other = await TenantAsync(_mongo.ConnectionString, withCountry: true);
        var person = await SignedInAsync(api, client, _mongo.ConnectionString, tenant);
        var token = Guid.NewGuid().ToString("N")[..8];
        var bulgaria = RegistrySeed.CountryOf("Bulgaria", "BG");
        var own = await RegistrySeed.AthleteAsync(
            _mongo.ConnectionString,
            tenant,
            $"Мария Тенант {token}",
            bulgaria,
            $"Maria Tenant {token}",
            "12"
        );
        var theirs = await RegistrySeed.AthleteAsync(
            _mongo.ConnectionString,
            other,
            $"Мария Друга {token}",
            bulgaria,
            $"Maria Other {token}",
            "777"
        );
        var horse = await RegistrySeed.HorseAsync(
            _mongo.ConnectionString,
            other,
            $"Далечен {token}",
            $"Faraway {token}",
            "ABC1"
        );
        var ownHorse = await RegistrySeed.HorseAsync(_mongo.ConnectionString, tenant, $"Свой {token}", null, "XYZ-9");
        var numericHorse = await RegistrySeed.HorseAsync(
            _mongo.ConnectionString,
            tenant,
            $"Цифров {token}",
            null,
            "0012"
        );
        return new Scene(ConsoleUi.Open(api, person), token, own, theirs, horse, ownHorse, numericHorse);
    }

    sealed class Scene : IAsyncDisposable
    {
        public Scene(ConsoleUi ui, string token, Guid own, Guid theirs, Guid horse, Guid ownHorse, Guid numericHorse)
        {
            NumericHorse = numericHorse;
            OwnHorse = ownHorse;
            Ui = ui;
            Token = token;
            Own = own;
            Theirs = theirs;
            Horse = horse;
        }

        public ConsoleUi Ui { get; }
        public string Token { get; }
        public Guid Own { get; }
        public Guid Theirs { get; }
        public Guid Horse { get; }
        public Guid OwnHorse { get; }
        public Guid NumericHorse { get; }

        public ValueTask DisposeAsync()
        {
            return Ui.DisposeAsync();
        }
    }
}

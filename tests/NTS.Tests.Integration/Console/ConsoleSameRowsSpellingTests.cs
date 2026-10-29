using NTS.Contracts.Features.Console.Setup;
using NTS.Tests.Integration.Harness;
using static NTS.Tests.Integration.Harness.TenancySeed;

namespace NTS.Tests.Integration;

/// <summary>
/// The registry's guard against a second copy in the scripts and spellings a name is given (#635, ADR-0010): an Athlete or a
/// Horse about to be registered or renamed is the same as a row when its name is the row's in the other script by the official
/// transliteration, and is like it when its name is a spelling of the row's by the looser key. A near-miss is not a warning.
/// Over the real Api in this process, with the services of the Ui built as the app builds them. The names are made up.
/// </summary>
public sealed class ConsoleSameRowsSpellingTests : IClassFixture<SharedApiFixture>
{
    readonly SharedApiFixture _shared;

    public ConsoleSameRowsSpellingTests(SharedApiFixture shared)
    {
        _shared = shared;
    }

    [Fact]
    public async Task An_Athlete_whose_name_is_a_row_in_the_other_script_is_the_same_and_one_whose_name_is_a_spelling_of_it_is_a_similar_one()
    {
        var api = _shared.Api;
        var scene = await SceneAsync(api);
        var rows = scene.Ui.Get<IConsoleSameRows>();

        var official = await rows.OfAthlete(new AthleteFormModel { Name = $"Yulia Testova {scene.Token}" });
        var spelled = await rows.OfAthlete(new AthleteFormModel { Name = $"Iuliia Testova {scene.Token}" });
        var inEnglish = await rows.OfAthlete(
            new AthleteFormModel { Name = "Нова", NameEnglish = $"Julia Testova {scene.Token}" }
        );

        Assert.Equal([(scene.Own, SameRowReason.Name, true)], official.Select(x => (x.Id, x.Reason, x.IsOwn)));
        Assert.Equal([(scene.Own, SameRowReason.SimilarName, true)], spelled.Select(x => (x.Id, x.Reason, x.IsOwn)));
        Assert.Equal([(scene.Own, SameRowReason.SimilarName, true)], inEnglish.Select(x => (x.Id, x.Reason, x.IsOwn)));
    }

    [Fact]
    public async Task A_near_miss_is_not_a_warning_and_neither_is_a_name_that_is_nothing_like()
    {
        var api = _shared.Api;
        var scene = await SceneAsync(api);
        var rows = scene.Ui.Get<IConsoleSameRows>();

        var nearMiss = await rows.OfAthlete(new AthleteFormModel { Name = $"Yulia Testov {scene.Token}" });
        var another = await rows.OfAthlete(new AthleteFormModel { Name = "Борис Примеров" });

        Assert.Empty(nearMiss);
        Assert.Empty(another);
    }

    [Fact]
    public async Task A_name_that_is_a_rows_under_another_FEI_ID_is_still_the_same_name_and_a_FEI_ID_that_is_a_rows_under_another_name_is_still_the_same_FEI_ID()
    {
        var api = _shared.Api;
        var scene = await SceneAsync(api);
        var rows = scene.Ui.Get<IConsoleSameRows>();

        var sameName = await rows.OfAthlete(
            new AthleteFormModel { Name = $"Yulia Testova {scene.Token}", FeiId = "10012399" }
        );
        var sameFeiId = await rows.OfAthlete(new AthleteFormModel { Name = "Съвсем друга", FeiId = "0010012345" });

        Assert.Equal([(scene.Own, SameRowReason.Name)], sameName.Select(x => (x.Id, x.Reason)));
        Assert.Equal([(scene.Own, SameRowReason.FeiId)], sameFeiId.Select(x => (x.Id, x.Reason)));
    }

    [Fact]
    public async Task A_Horse_is_the_same_as_a_row_in_the_other_script_and_the_row_being_renamed_is_not_the_same_as_itself()
    {
        var api = _shared.Api;
        var scene = await SceneAsync(api);
        var rows = scene.Ui.Get<IConsoleSameRows>();

        var latin = await rows.OfHorse(new HorseFormModel { Name = $"Test Ogan {scene.Token}" });
        var renamed = await rows.OfHorse(new HorseFormModel { Id = scene.OwnHorse, Name = $"Test Ogan {scene.Token}" });
        var athleteRenamed = await rows.OfAthlete(
            new AthleteFormModel { Id = scene.Own, Name = $"Yulia Testova {scene.Token}" }
        );

        Assert.Equal([(scene.OwnHorse, SameRowReason.Name)], latin.Select(x => (x.Id, x.Reason)));
        Assert.Empty(renamed);
        Assert.Empty(athleteRenamed);
    }

    async Task<Scene> SceneAsync(ApiFactory api)
    {
        using var client = ApiClients.Of(api);
        var tenant = await TenantAsync(_shared.MongoConnectionString, withCountry: true);
        var person = await SignedInAsync(api, client, _shared.MongoConnectionString, tenant);
        var token = Guid.NewGuid().ToString("N")[..8];
        var own = await RegistrySeed.AthleteAsync(
            _shared.MongoConnectionString,
            tenant,
            $"Юлия Тестова {token}",
            RegistrySeed.CountryOf("Bulgaria", "BG"),
            null,
            "10012345"
        );
        var ownHorse = await RegistrySeed.HorseAsync(_shared.MongoConnectionString, tenant, $"Тест Огън {token}");
        return new Scene(ConsoleUi.Open(api, person), token, own, ownHorse);
    }

    sealed class Scene : IAsyncDisposable
    {
        public Scene(ConsoleUi ui, string token, Guid own, Guid ownHorse)
        {
            Ui = ui;
            Token = token;
            Own = own;
            OwnHorse = ownHorse;
        }

        public ConsoleUi Ui { get; }
        public string Token { get; }
        public Guid Own { get; }
        public Guid OwnHorse { get; }

        public ValueTask DisposeAsync()
        {
            return Ui.DisposeAsync();
        }
    }
}

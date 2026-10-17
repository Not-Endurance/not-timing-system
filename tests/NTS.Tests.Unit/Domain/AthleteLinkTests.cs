using NTS.Contracts.Features.Console.Setup;
using NTS.Contracts.Setup.Models;
using NTS.Domain.Setup.Aggregates;

namespace NTS.Tests.Unit.Domain;

/// <summary>
/// An Athlete of the registry is linked to no account (#635, ADR-0010): the link pointed from the Athlete to a user, nothing read
/// it, and the user's own link to an Athlete lives with the user. No code, model or form refers to it.
/// </summary>
public sealed class AthleteLinkTests
{
    [Fact]
    public void An_Athlete_has_no_account_in_the_domain_the_model_or_the_form()
    {
        Assert.Null(typeof(Athlete).GetProperty("User"));
        Assert.DoesNotContain(
            typeof(Athlete).GetConstructors().SelectMany(x => x.GetParameters()),
            x => string.Equals(x.Name, "user", StringComparison.OrdinalIgnoreCase)
        );
        Assert.Null(typeof(AthleteModel).GetProperty("User"));
        Assert.Null(typeof(AthleteFormModel).GetProperty("User"));
    }

    [Fact]
    public void An_Athlete_is_made_of_its_names_FEI_ID_Country_and_Club_and_its_id()
    {
        var country = new NTS.Domain.Aggregates.Country(TestId.Of(1), "Bulgaria", "BG", "BUL", "bg-BG");
        var id = TestId.Of(2);

        var athlete = new Athlete("Мариела Тестова", "Mariela Testova", "10012345", country, null, id);
        var model = AthleteModel.From(athlete);

        Assert.Equal(id, athlete.Id);
        Assert.Equal("10012345", athlete.FeiId);
        Assert.Equal(id, model.MapToEntity().Id);
        Assert.Equal("Mariela Testova", model.MapToEntity().NameEnglish);
    }
}

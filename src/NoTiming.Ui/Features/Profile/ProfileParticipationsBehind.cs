using MudBlazor;
using Not.Blazor.Components.Abstractions;
using NoTiming.Ui.Features.Participants;
using NTS.Contracts.Features.Participants;
using NTS.Contracts.Features.Profile;

namespace NoTiming.Ui.Features.Profile;

/// <summary>
/// The Participations in the profile of the person (#639, ADR-0010): the Athlete they bookmark as themselves, which is not a
/// claim and grants nothing, with the Participations of that Athlete, and their Horses: the ones their Athlete has ridden,
/// which follow from the Participations and are not removed, and the ones they add, which are. A person with no Athlete can
/// still add Horses.
/// </summary>
public class ProfileParticipationsBehind : NStatefulComponent
{
    IReadOnlyList<ParticipantName> _ridden = [];
    Guid? _riddenFor;

    [Inject]
    IWitnessProfileContext ProfileContext { get; set; } = default!;

    [Inject]
    IParticipationHistory History { get; set; } = default!;

    [Inject]
    IDialogService DialogService { get; set; } = default!;

    protected ParticipantName? Athlete => ProfileContext.Profile?.Athlete;

    /// <summary>The Horses that are shown: the ones the Athlete has ridden, the latest first, and then the ones that were only added.</summary>
    protected IReadOnlyList<ShownHorse> Horses
    {
        get
        {
            var added = ProfileContext.Profile?.Horses ?? [];
            return
            [
                .. _ridden.Select(x => new ShownHorse(x, added.Any(h => h.Id == x.Id))),
                .. added.Where(x => _ridden.All(h => h.Id != x.Id)).Select(x => new ShownHorse(x, true)),
            ];
        }
    }

    protected override async Task OnInitializedAsync()
    {
        await Observe(ProfileContext);
        await ReadRidden();
    }

    /// <summary>The Horses ridden are read again when the Athlete is not the one they were read for.</summary>
    protected override async Task OnBeforeRenderAsync()
    {
        await ReadRidden();
    }

    protected async Task LinkAthlete(ParticipantName? athlete)
    {
        try
        {
            if (athlete != null)
            {
                await ProfileContext.LinkAthlete(athlete.Id);
            }
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task UnlinkAthlete()
    {
        try
        {
            await ProfileContext.UnlinkAthlete();
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task AddHorse(ParticipantName? horse)
    {
        try
        {
            if (horse != null)
            {
                await ProfileContext.AddHorse(horse.Id);
            }
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected async Task RemoveHorse(ParticipantName horse)
    {
        try
        {
            await ProfileContext.RemoveHorse(horse.Id);
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    /// <summary>The Participations of the Horse, in a dialog.</summary>
    protected async Task Open(ParticipantName horse)
    {
        try
        {
            var parameters = new DialogParameters<ParticipantDialog> { { x => x.Participant, horse } };
            var dialog = await DialogService.ShowAsync<ParticipantDialog>(
                horse.DisplayName,
                parameters,
                new DialogOptions { MaxWidth = MaxWidth.Large, FullWidth = true }
            );
            await dialog.Result;
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected static string RemoveText(ParticipantName participant)
    {
        return string.Format(Remove__string, participant.DisplayName);
    }

    async Task ReadRidden()
    {
        var athlete = Athlete?.Id;
        if (athlete == _riddenFor)
        {
            return;
        }

        _riddenFor = athlete;
        _ridden = [];
        if (athlete == null)
        {
            return;
        }

        try
        {
            var ridden = await History.HorsesRiddenBy(athlete.Value, CancellationToken.None);
            if (_riddenFor == athlete)
            {
                _ridden = ridden;
            }
        }
        catch (Exception ex)
        {
            Handle(ex);
        }
    }

    protected sealed class ShownHorse
    {
        public ShownHorse(ParticipantName horse, bool isRemovable)
        {
            Horse = horse;
            IsRemovable = isRemovable;
        }

        public ParticipantName Horse { get; }

        /// <summary>Whether the person added the Horse, which they can take away; a Horse that only follows from the rides of the Athlete cannot be.</summary>
        public bool IsRemovable { get; }
    }
}

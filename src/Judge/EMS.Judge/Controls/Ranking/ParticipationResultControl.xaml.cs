using System.Windows;

namespace EMS.Judge.Controls.Ranking;

public partial class ParticipationResultControl
{
    public ParticipationResultControl()
    {
        InitializeComponent();
    }

    public ParticipationResultModel Participation
    {
        get => (ParticipationResultModel)this.GetValue(PARTICIPATION_PROPERTY);
        set => this.SetValue(PARTICIPATION_PROPERTY, value);
    }

    public static readonly DependencyProperty PARTICIPATION_PROPERTY =
        DependencyProperty.Register(
            nameof(Participation),
            typeof(ParticipationResultModel),
            typeof(ParticipationResultControl),
            new PropertyMetadata(OnParticipationChanged));

    private static void OnParticipationChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var grid = (ParticipationResultControl)sender;
        var participation = (ParticipationResultModel) args.NewValue;
        grid.Populate(participation);
    }

    private void Populate(ParticipationResultModel participation)
    {
        this.NumberText.Text = participation.Participant.Number;
        this.AthleteNameText.Text = participation.Participant.Athlete.Name;
        this.AthleteFeiIdText.Text = participation.Participant.Athlete.FeiId;
        this.CountryText.Text = participation.Participant.Athlete.Country.Name;
        this.HorseNameText.Text = participation.Participant.Horse.Name;
        this.AthleteClub.Text = participation.Participant.Athlete.Club;
        this.RankText.Text = participation.Rank.ToString();
        this.RideTime.Text = participation.RideTime;
        this.RecTime.Text = participation.RecTime;
        this.TotalTime.Text = participation.TotalTime;
        this.TotalAverageSpeedString.Text = participation.TotalAverageSpeed;
        if (participation.DisqualifiedVisibility == Visibility.Visible)
        {
            this.TotalAverageSpeedBorder.Visibility = Visibility.Collapsed;
        }
        this.NotQualifiedText.Text = participation.NotQualifiedText;
        this.DisqualifiedContainer.Visibility = participation.DisqualifiedVisibility;
        this.ParticipationGrid.Table.ItemsSource = participation.Performances;
        this.ParticipationGrid.PrintButton.Visibility = Visibility.Collapsed;
    }
}

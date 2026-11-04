/*
 * !!!!!!!!!!!!!!!!!!!!! READ CAREFULLY BEFORE REPLACING WITH NEWLY GENERATED FILE !!!!!!!!!!!!!!!!!!!!!!!!!!!!
 * The following changes are made to this file in order to produce the expected XML
 * - Add DataType = "date" to all DateTime properties - this formats the output date as a simple date string, rather than UTF representation
 * - Change *DateSpecified properties to check if their Date property is not default automatically and return true
 * - Change *SpeedSpecified properties to check if their Speed property is not default automatiicaly and return true
 * - Change RankSpecified property in individual result to check if Status == "R" and Rank is not default
 * !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
 */

using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Xml.Serialization;
#pragma warning disable CS0169 // Field is never used

// ReSharper disable InconsistentNaming
// ReSharper disable ArrangeAccessorOwnerBody
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace NTS.Judge.Features.Core.Rankings.FeiExport;

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(AnonymousType = true, Namespace = "http://www.fei.org/Result")]
[XmlRoot(Namespace = "http://www.fei.org/Result", IsNullable = false)]
public class HorseSport
{
    private ctGenerated generatedField;

    private ctIssuer issuerField;

    private ctShowResultType eventResultField;

    /// <remarks/>
    public ctGenerated Generated
    {
        get { return generatedField; }
        set { generatedField = value; }
    }

    /// <remarks/>
    public ctIssuer Issuer
    {
        get { return issuerField; }
        set { issuerField = value; }
    }

    /// <remarks/>
    public ctShowResultType EventResult
    {
        get { return eventResultField; }
        set { eventResultField = value; }
    }
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.fei.org/Result")]
public class ctGenerated
{
    private DateTime dateField;

    private string softwareField;

    private string softwareVersionField;

    private string organizationField;

    /// <remarks/>
    [XmlIgnore] //Date is added manually after serialization because XmlSerializer does not allow me to format dates differently and here it's required in ISO 8601 format
    [XmlAttribute(DataType = "date")]
    public DateTime Date
    {
        get { return dateField; }
        set { dateField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string Software
    {
        get { return softwareField; }
        set { softwareField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string SoftwareVersion
    {
        get { return softwareVersionField; }
        set { softwareVersionField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string Organization
    {
        get { return organizationField; }
        set { organizationField = value; }
    }
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.fei.org/Result")]
public class ctEnduranceTotalTeam
{
    private string timeField;

    /// <remarks/>
    [XmlAttribute]
    public string Time
    {
        get { return timeField; }
        set { timeField = value; }
    }
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.fei.org/Result")]
public class ctPositionTeam
{
    private string statusField;

    private int rankField;

    private bool rankFieldSpecified;

    /// <remarks/>
    [XmlAttribute]
    public string Status
    {
        get { return statusField; }
        set { statusField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public int Rank
    {
        get { return rankField; }
        set { rankField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool RankSpecified => Rank != default;
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.fei.org/Result")]
public class ctTeam
{
    private int numberField;

    private bool numberFieldSpecified;

    private string nfField;

    /// <remarks/>
    [XmlAttribute]
    public int Number
    {
        get { return numberField; }
        set { numberField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool NumberSpecified
    {
        get { return numberFieldSpecified; }
        set { numberFieldSpecified = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string NF
    {
        get { return nfField; }
        set { nfField = value; }
    }
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.fei.org/Result")]
public class ctEnduranceTeamResult
{
    private ctTeam teamField;

    private ctPositionTeam positionField;

    private ctPrizeMoney prizeMoneyField;

    private ctEnduranceTotalTeam totalField;

    /// <remarks/>
    public ctTeam Team
    {
        get { return teamField; }
        set { teamField = value; }
    }

    /// <remarks/>
    public ctPositionTeam Position
    {
        get { return positionField; }
        set { positionField = value; }
    }

    /// <remarks/>
    public ctPrizeMoney PrizeMoney
    {
        get { return prizeMoneyField; }
        set { prizeMoneyField = value; }
    }

    /// <remarks/>
    public ctEnduranceTotalTeam Total
    {
        get { return totalField; }
        set { totalField = value; }
    }
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.fei.org/Result")]
public class ctPrizeMoney
{
    private decimal valueField;

    private string inKindField;

    private decimal inKindValueField;

    private bool inKindValueFieldSpecified;

    /// <remarks/>
    [XmlAttribute]
    public decimal Value
    {
        get { return valueField; }
        set { valueField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string InKind
    {
        get { return inKindField; }
        set { inKindField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public decimal InKindValue
    {
        get { return inKindValueField; }
        set { inKindValueField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool InKindValueSpecified
    {
        get { return inKindValueFieldSpecified; }
        set { inKindValueFieldSpecified = value; }
    }
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.fei.org/Result")]
public class ctEnduranceTotal
{
    private string timeField;

    private decimal averageSpeedField;

    private bool averageSpeedFieldSpecified;

    /// <remarks/>
    [XmlAttribute]
    public string Time
    {
        get { return timeField; }
        set { timeField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public decimal AverageSpeed
    {
        get { return averageSpeedField; }
        set { averageSpeedField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool AverageSpeedSpecified => AverageSpeed != default;
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.fei.org/Result")]
public class ctEnduranceVetInspection
{
    private stEnduranceVetTypeCode typeField;

    private string eliminationCodeField;

    private int heartRateField;

    private bool heartRateFieldSpecified;

    private string recoveryTimeField;

    private stEnduranceDuringReinspection duringReinspectionField;

    private bool duringReinspectionFieldSpecified;

    /// <remarks/>
    [XmlAttribute]
    public stEnduranceVetTypeCode Type
    {
        get { return typeField; }
        set { typeField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string EliminationCode
    {
        get { return eliminationCodeField; }
        set { eliminationCodeField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public int HeartRate
    {
        get { return heartRateField; }
        set { heartRateField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool HeartRateSpecified
    {
        get { return heartRateFieldSpecified; }
        set { heartRateFieldSpecified = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string RecoveryTime
    {
        get { return recoveryTimeField; }
        set { recoveryTimeField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public stEnduranceDuringReinspection DuringReinspection
    {
        get { return duringReinspectionField; }
        set { duringReinspectionField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool DuringReinspectionSpecified
    {
        get { return duringReinspectionFieldSpecified; }
        set { duringReinspectionFieldSpecified = value; }
    }
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[XmlType(Namespace = "http://www.fei.org/Result")]
public enum stEnduranceVetTypeCode
{
    /// <remarks/>
    Standard,

    /// <remarks/>
    Final,
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[XmlType(Namespace = "http://www.fei.org/Result")]
public enum stEnduranceDuringReinspection
{
    /// <remarks/>
    no,

    /// <remarks/>
    [XmlEnum("yes-comp")]
    yescomp,

    /// <remarks/>
    [XmlEnum("yes-req")]
    yesreq,
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.fei.org/Result")]
public class ctEndurancePhaseResultScore
{
    private decimal phaseAverageSpeedField;

    private bool phaseAverageSpeedFieldSpecified;

    private string phaseTimeField;

    private int rankField;

    private bool rankFieldSpecified;

    private string recoveryTimeField;

    /// <remarks/>
    [XmlAttribute]
    public decimal PhaseAverageSpeed
    {
        get { return phaseAverageSpeedField; }
        set { phaseAverageSpeedField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool PhaseAverageSpeedSpecified => PhaseAverageSpeed != default;

    /// <remarks/>
    [XmlAttribute]
    public string PhaseTime
    {
        get { return phaseTimeField; }
        set { phaseTimeField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public int Rank
    {
        get { return rankField; }
        set { rankField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool RankSpecified => Rank != default;

    /// <remarks/>
    [XmlAttribute]
    public string RecoveryTime
    {
        get { return recoveryTimeField; }
        set { recoveryTimeField = value; }
    }
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.fei.org/Result")]
public class ctEndurancePhaseResult
{
    private ctEndurancePhaseResultScore resultField;

    private ctEnduranceVetInspection vetInspectionField;

    private int numberField;

    /// <remarks/>
    public ctEndurancePhaseResultScore Result
    {
        get { return resultField; }
        set { resultField = value; }
    }

    /// <remarks/>
    public ctEnduranceVetInspection VetInspection
    {
        get { return vetInspectionField; }
        set { vetInspectionField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public int Number
    {
        get { return numberField; }
        set { numberField = value; }
    }
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.fei.org/Result")]
public class ctEnduranceDayResult
{
    private ctEndurancePhaseResult[] phaseField;

    private int numberField;

    private DateTime dateField;

    private bool dateFieldSpecified;

    /// <remarks/>
    [XmlElement("Phase")]
    public ctEndurancePhaseResult[] Phase
    {
        get { return phaseField; }
        set { phaseField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public int Number
    {
        get { return numberField; }
        set { numberField = value; }
    }

    /// <remarks/>
    [XmlAttribute(DataType = "date")]
    public DateTime Date
    {
        get { return dateField; }
        set { dateField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool DateSpecified => Date != default;
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.fei.org/Result")]
public class ctEnduranceFirstVetInspection
{
    private stEnduranceFirstVetTypeCode typeField;

    private string eliminationCodeField;

    /// <remarks/>
    [XmlAttribute]
    public stEnduranceFirstVetTypeCode Type
    {
        get { return typeField; }
        set { typeField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string EliminationCode
    {
        get { return eliminationCodeField; }
        set { eliminationCodeField = value; }
    }
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[XmlType(Namespace = "http://www.fei.org/Result")]
public enum stEnduranceFirstVetTypeCode
{
    /// <remarks/>
    First,
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.fei.org/Result")]
public class ctPositionIndiv
{
    private string statusField;

    private int rankField;

    private bool rankFieldSpecified;

    private string complementField;

    private string complementDataField;

    /// <remarks/>
    [XmlAttribute]
    public string Status
    {
        get { return statusField; }
        set { statusField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public int Rank
    {
        get { return rankField; }
        set { rankField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool RankSpecified => Rank != default && Status == "R";

    /// <remarks/>
    [XmlAttribute]
    public string Complement
    {
        get { return complementField; }
        set { complementField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string ComplementData
    {
        get { return complementDataField; }
        set { complementDataField = value; }
    }
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.fei.org/Result")]
public class ctEnduranceComplement
{
    private bool bestConditionField;

    /// <remarks/>
    [XmlAttribute]
    public bool BestCondition
    {
        get { return bestConditionField; }
        set { bestConditionField = value; }
    }
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.fei.org/Result")]
public class ctRefTeam
{
    private string nameField;

    /// <remarks/>
    [XmlAttribute]
    public string Name
    {
        get { return nameField; }
        set { nameField = value; }
    }
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.fei.org/Result")]
public class ctHorse
{
    private string fEIIDField;

    private string nameField;

    private int headNumberField;

    private bool headNumberFieldSpecified;

    private string nFIDField;

    /// <remarks/>
    [XmlAttribute]
    public string FEIID
    {
        get { return fEIIDField; }
        set { fEIIDField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string Name
    {
        get { return nameField; }
        set { nameField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public int HeadNumber
    {
        get { return headNumberField; }
        set { headNumberField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool HeadNumberSpecified
    {
        get { return headNumberFieldSpecified; }
        set { headNumberFieldSpecified = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string NFID
    {
        get { return nFIDField; }
        set { nFIDField = value; }
    }
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.fei.org/Result")]
public class ctEnduranceAthlete
{
    private int fEIIDField;

    private string firstNameField;

    private string familyNameField;

    private string competingForField;

    private string nFIDField;

    private int athleteNumberField;

    private bool athleteNumberFieldSpecified;

    /// <remarks/>
    [XmlAttribute]
    public int FEIID
    {
        get { return fEIIDField; }
        set { fEIIDField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string FirstName
    {
        get { return firstNameField; }
        set { firstNameField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string FamilyName
    {
        get { return familyNameField; }
        set { familyNameField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string CompetingFor
    {
        get { return competingForField; }
        set { competingForField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string NFID
    {
        get { return nFIDField; }
        set { nFIDField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public int AthleteNumber
    {
        get { return athleteNumberField; }
        set { athleteNumberField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool AthleteNumberSpecified
    {
        get { return athleteNumberFieldSpecified; }
        set { athleteNumberFieldSpecified = value; }
    }
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.fei.org/Result")]
public class ctEnduranceIndivResult
{
    private ctEnduranceAthlete athleteField;

    private ctHorse horseField;

    private ctPrizeMoney prizeMoneyField;

    private ctRefTeam teamField;

    private ctEnduranceComplement complementField;

    private ctPositionIndiv positionField;

    private ctEnduranceFirstVetInspection vetInspectionField;

    private ctEnduranceDayResult[] phasesField;

    private ctEnduranceTotal totalField;

    /// <remarks/>
    public ctEnduranceAthlete Athlete
    {
        get { return athleteField; }
        set { athleteField = value; }
    }

    /// <remarks/>
    public ctHorse Horse
    {
        get { return horseField; }
        set { horseField = value; }
    }

    /// <remarks/>
    public ctPrizeMoney PrizeMoney
    {
        get { return prizeMoneyField; }
        set { prizeMoneyField = value; }
    }

    /// <remarks/>
    public ctRefTeam Team
    {
        get { return teamField; }
        set { teamField = value; }
    }

    /// <remarks/>
    public ctEnduranceComplement Complement
    {
        get { return complementField; }
        set { complementField = value; }
    }

    /// <remarks/>
    public ctPositionIndiv Position
    {
        get { return positionField; }
        set { positionField = value; }
    }

    /// <remarks/>
    public ctEnduranceFirstVetInspection VetInspection
    {
        get { return vetInspectionField; }
        set { vetInspectionField = value; }
    }

    /// <remarks/>
    [XmlArrayItem("Day", IsNullable = false)]
    public ctEnduranceDayResult[] Phases
    {
        get { return phasesField; }
        set { phasesField = value; }
    }

    /// <remarks/>
    public ctEnduranceTotal Total
    {
        get { return totalField; }
        set { totalField = value; }
    }
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.fei.org/Result")]
public class ctEnduranceParticipations
{
    private ctEnduranceIndivResult[] participationField;

    private ctEnduranceTeamResult[] teamParticipationField;

    /// <remarks/>
    [XmlElement("Participation")]
    public ctEnduranceIndivResult[] Participation
    {
        get { return participationField; }
        set { participationField = value; }
    }

    /// <remarks/>
    [XmlElement("TeamParticipation")]
    public ctEnduranceTeamResult[] TeamParticipation
    {
        get { return teamParticipationField; }
        set { teamParticipationField = value; }
    }
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.fei.org/Result")]
public class ctPrizeMoneyPlace
{
    private int placeField;

    private bool placeFieldSpecified;

    private decimal amountField;

    private bool amountFieldSpecified;

    private string inKindField;

    private decimal inKindValueField;

    private bool inKindValueFieldSpecified;

    /// <remarks/>
    [XmlAttribute]
    public int Place
    {
        get { return placeField; }
        set { placeField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool PlaceSpecified
    {
        get { return placeFieldSpecified; }
        set { placeFieldSpecified = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public decimal Amount
    {
        get { return amountField; }
        set { amountField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool AmountSpecified
    {
        get { return amountFieldSpecified; }
        set { amountFieldSpecified = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string InKind
    {
        get { return inKindField; }
        set { inKindField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public decimal InKindValue
    {
        get { return inKindValueField; }
        set { inKindValueField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool InKindValueSpecified
    {
        get { return inKindValueFieldSpecified; }
        set { inKindValueFieldSpecified = value; }
    }
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.fei.org/Result")]
public class ctEndurancePrizeMoneyDetail
{
    private ctPrizeMoneyPlace[] prizeField;

    private string currencyField;

    private decimal totalField;

    private bool totalFieldSpecified;

    /// <remarks/>
    [XmlElement("Prize")]
    public ctPrizeMoneyPlace[] Prize
    {
        get { return prizeField; }
        set { prizeField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string Currency
    {
        get { return currencyField; }
        set { currencyField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public decimal Total
    {
        get { return totalField; }
        set { totalField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool TotalSpecified
    {
        get { return totalFieldSpecified; }
        set { totalFieldSpecified = value; }
    }
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.fei.org/Result")]
public class ctPhaseDetailDetail
{
    private int numberOfStarterField;

    private int numberOfFinisherField;

    /// <remarks/>
    [XmlAttribute]
    public int NumberOfStarter
    {
        get { return numberOfStarterField; }
        set { numberOfStarterField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public int NumberOfFinisher
    {
        get { return numberOfFinisherField; }
        set { numberOfFinisherField = value; }
    }
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.fei.org/Result")]
public class ctPhaseDetailTime
{
    private DateTime startField;

    private DateTime endField;

    /// <remarks/>
    [XmlAttribute]
    public DateTime Start
    {
        get { return startField; }
        set { startField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public DateTime End
    {
        get { return endField; }
        set { endField = value; }
    }
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.fei.org/Result")]
public class ctEndurancePhaseDetailCourse
{
    private int distanceField;

    private bool distanceFieldSpecified;

    private int holdTimeField;

    private bool holdTimeFieldSpecified;

    private bool compulsoryReinspectionField;

    private bool compulsoryReinspectionFieldSpecified;

    /// <remarks/>
    [XmlAttribute]
    public int Distance
    {
        get { return distanceField; }
        set { distanceField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool DistanceSpecified
    {
        get { return distanceFieldSpecified; }
        set { distanceFieldSpecified = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public int HoldTime
    {
        get { return holdTimeField; }
        set { holdTimeField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool HoldTimeSpecified
    {
        get { return holdTimeFieldSpecified; }
        set { holdTimeFieldSpecified = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public bool CompulsoryReinspection
    {
        get { return compulsoryReinspectionField; }
        set { compulsoryReinspectionField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool CompulsoryReinspectionSpecified
    {
        get { return compulsoryReinspectionFieldSpecified; }
        set { compulsoryReinspectionFieldSpecified = value; }
    }
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.fei.org/Result")]
public class ctEndurancePhaseDetail
{
    private ctEndurancePhaseDetailCourse courseField;

    private ctPhaseDetailTime executionTimeField;

    private ctPhaseDetailDetail detailField;

    private int numberField;

    private DateTime startHourField;

    private bool startHourFieldSpecified;

    /// <remarks/>
    public ctEndurancePhaseDetailCourse Course
    {
        get { return courseField; }
        set { courseField = value; }
    }

    /// <remarks/>
    public ctPhaseDetailTime ExecutionTime
    {
        get { return executionTimeField; }
        set { executionTimeField = value; }
    }

    /// <remarks/>
    public ctPhaseDetailDetail Detail
    {
        get { return detailField; }
        set { detailField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public int Number
    {
        get { return numberField; }
        set { numberField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public DateTime StartHour
    {
        get { return startHourField; }
        set { startHourField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool StartHourSpecified
    {
        get { return startHourFieldSpecified; }
        set { startHourFieldSpecified = value; }
    }
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.fei.org/Result")]
public class ctEnduranceDayDetail
{
    private ctEndurancePhaseDetail[] phaseField;

    private int numberField;

    private DateTime dateField;

    /// <remarks/>
    [XmlElement("Phase")]
    public ctEndurancePhaseDetail[] Phase
    {
        get { return phaseField; }
        set { phaseField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public int Number
    {
        get { return numberField; }
        set { numberField = value; }
    }

    /// <remarks/>
    [XmlAttribute(DataType = "date")]
    public DateTime Date
    {
        get { return dateField; }
        set { dateField = value; }
    }
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.fei.org/Result")]
public class ctEndurancePhasesDetail
{
    private ctEnduranceDayDetail[] dayField;

    private int totalNumberField;

    private bool totalNumberFieldSpecified;

    /// <remarks/>
    [XmlElement("Day")]
    public ctEnduranceDayDetail[] Day
    {
        get { return dayField; }
        set { dayField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public int TotalNumber
    {
        get { return totalNumberField; }
        set { totalNumberField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool TotalNumberSpecified
    {
        get { return totalNumberFieldSpecified; }
        set { totalNumberFieldSpecified = value; }
    }
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.fei.org/Result")]
public class ctEnduranceDescription
{
    private ctEndurancePhasesDetail phasesField;

    private ctEndurancePrizeMoneyDetail prizesField;

    /// <remarks/>
    public ctEndurancePhasesDetail Phases
    {
        get { return phasesField; }
        set { phasesField = value; }
    }

    /// <remarks/>
    public ctEndurancePrizeMoneyDetail Prizes
    {
        get { return prizesField; }
        set { prizesField = value; }
    }
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.fei.org/Result")]
public class ctEnduranceCompetition
{
    private ctEnduranceDescription descriptionField;

    private ctEnduranceParticipations participationListField;

    private string fEIIDField;

    private string nFIDField;

    private string scheduleCompetitionNrField;

    private string ruleField;

    private string nameField;

    private bool teamField;

    private bool teamFieldSpecified;

    private DateTime startDateField;

    private bool startDateFieldSpecified;

    private DateTime endDateField;

    private bool endDateFieldSpecified;

    /// <remarks/>
    public ctEnduranceDescription Description
    {
        get { return descriptionField; }
        set { descriptionField = value; }
    }

    /// <remarks/>
    public ctEnduranceParticipations ParticipationList
    {
        get { return participationListField; }
        set { participationListField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string FEIID
    {
        get { return fEIIDField; }
        set { fEIIDField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string NFID
    {
        get { return nFIDField; }
        set { nFIDField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string ScheduleCompetitionNr
    {
        get { return scheduleCompetitionNrField; }
        set { scheduleCompetitionNrField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string Rule
    {
        get { return ruleField; }
        set { ruleField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string Name
    {
        get { return nameField; }
        set { nameField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public bool Team
    {
        get { return teamField; }
        set { teamField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool TeamSpecified
    {
        get { return teamFieldSpecified; }
        set { teamFieldSpecified = value; }
    }

    /// <remarks/>
    [XmlAttribute(DataType = "date")]
    public DateTime StartDate
    {
        get { return startDateField; }
        set { startDateField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool StartDateSpecified => StartDate != default;

    /// <remarks/>
    [XmlAttribute]
    public DateTime EndDate
    {
        get { return endDateField; }
        set { endDateField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool EndDateSpecified => EndDate != default;
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.fei.org/Result")]
public class ctOfficialVeterinarianRole
{
    private int fEIIDField;

    private bool fEIIDFieldSpecified;

    private string nFIDField;

    private string firstNameField;

    private string familyNameField;

    private string nfField;

    private stVeterinarianRole roleField;

    /// <remarks/>
    [XmlAttribute]
    public int FEIID
    {
        get { return fEIIDField; }
        set { fEIIDField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool FEIIDSpecified
    {
        get { return fEIIDFieldSpecified; }
        set { fEIIDFieldSpecified = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string NFID
    {
        get { return nFIDField; }
        set { nFIDField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string FirstName
    {
        get { return firstNameField; }
        set { firstNameField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string FamilyName
    {
        get { return familyNameField; }
        set { familyNameField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string NF
    {
        get { return nfField; }
        set { nfField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public stVeterinarianRole Role
    {
        get { return roleField; }
        set { roleField = value; }
    }
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[XmlType(Namespace = "http://www.fei.org/Result")]
public enum stVeterinarianRole
{
    /// <remarks/>
    President,

    /// <remarks/>
    Foreign,

    /// <remarks/>
    Assistant,
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.fei.org/Result")]
public class ctOfficialSteward
{
    private int fEIIDField;

    private bool fEIIDFieldSpecified;

    private string nFIDField;

    private string firstNameField;

    private string familyNameField;

    private string nfField;

    private stOfficialStewardRole roleField;

    /// <remarks/>
    [XmlAttribute]
    public int FEIID
    {
        get { return fEIIDField; }
        set { fEIIDField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool FEIIDSpecified
    {
        get { return fEIIDFieldSpecified; }
        set { fEIIDFieldSpecified = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string NFID
    {
        get { return nFIDField; }
        set { nFIDField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string FirstName
    {
        get { return firstNameField; }
        set { firstNameField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string FamilyName
    {
        get { return familyNameField; }
        set { familyNameField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string NF
    {
        get { return nfField; }
        set { nfField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public stOfficialStewardRole Role
    {
        get { return roleField; }
        set { roleField = value; }
    }
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[XmlType(Namespace = "http://www.fei.org/Result")]
public enum stOfficialStewardRole
{
    /// <remarks/>
    Chief,

    /// <remarks/>
    Assistant,
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.fei.org/Result")]
public class ctTechnicalDelegate
{
    private int fEIIDField;

    private bool fEIIDFieldSpecified;

    private string nFIDField;

    private string firstNameField;

    private string familyNameField;

    private string nfField;

    /// <remarks/>
    [XmlAttribute]
    public int FEIID
    {
        get { return fEIIDField; }
        set { fEIIDField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool FEIIDSpecified
    {
        get { return fEIIDFieldSpecified; }
        set { fEIIDFieldSpecified = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string NFID
    {
        get { return nFIDField; }
        set { nFIDField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string FirstName
    {
        get { return firstNameField; }
        set { firstNameField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string FamilyName
    {
        get { return familyNameField; }
        set { familyNameField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string NF
    {
        get { return nfField; }
        set { nfField = value; }
    }
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.fei.org/Result")]
public class ctOfficialJudge
{
    private int fEIIDField;

    private bool fEIIDFieldSpecified;

    private string nFIDField;

    private string firstNameField;

    private string familyNameField;

    private string nfField;

    private stOfficialJudgeRole roleField;

    /// <remarks/>
    [XmlAttribute]
    public int FEIID
    {
        get { return fEIIDField; }
        set { fEIIDField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool FEIIDSpecified
    {
        get { return fEIIDFieldSpecified; }
        set { fEIIDFieldSpecified = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string NFID
    {
        get { return nFIDField; }
        set { nFIDField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string FirstName
    {
        get { return firstNameField; }
        set { firstNameField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string FamilyName
    {
        get { return familyNameField; }
        set { familyNameField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string NF
    {
        get { return nfField; }
        set { nfField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public stOfficialJudgeRole Role
    {
        get { return roleField; }
        set { roleField = value; }
    }
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[XmlType(Namespace = "http://www.fei.org/Result")]
public enum stOfficialJudgeRole
{
    /// <remarks/>
    President,

    /// <remarks/>
    Foreign,

    /// <remarks/>
    Member,
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.fei.org/Result")]
public class ctEnduranceOfficial
{
    private ctOfficialJudge[] judgeField;

    private ctTechnicalDelegate[] technicalDelegateField;

    private ctOfficialSteward[] stewardField;

    private ctOfficialVeterinarianRole[] veterinarianField;

    /// <remarks/>
    [XmlElement("Judge")]
    public ctOfficialJudge[] Judge
    {
        get { return judgeField; }
        set { judgeField = value; }
    }

    /// <remarks/>
    [XmlElement("TechnicalDelegate")]
    public ctTechnicalDelegate[] TechnicalDelegate
    {
        get { return technicalDelegateField; }
        set { technicalDelegateField = value; }
    }

    /// <remarks/>
    [XmlElement("Steward")]
    public ctOfficialSteward[] Steward
    {
        get { return stewardField; }
        set { stewardField = value; }
    }

    /// <remarks/>
    [XmlElement("Veterinarian")]
    public ctOfficialVeterinarianRole[] Veterinarian
    {
        get { return veterinarianField; }
        set { veterinarianField = value; }
    }
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.fei.org/Result")]
public class ctEnduranceEvent
{
    private ctEnduranceOfficial officialsField;

    private ctEnduranceCompetition[] competitionsField;

    private string fEIIDField;

    private string nFIDField;

    private string codeField;

    private string nfField;

    private string nameField;

    private DateTime startDateField;

    private bool startDateFieldSpecified;

    private DateTime endDateField;

    private bool endDateFieldSpecified;

    /// <remarks/>
    public ctEnduranceOfficial Officials
    {
        get { return officialsField; }
        set { officialsField = value; }
    }

    /// <remarks/>
    [XmlArrayItem("Competition", IsNullable = false)]
    public ctEnduranceCompetition[] Competitions
    {
        get { return competitionsField; }
        set { competitionsField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string FEIID
    {
        get { return fEIIDField; }
        set { fEIIDField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string NFID
    {
        get { return nFIDField; }
        set { nFIDField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string Code
    {
        get { return codeField; }
        set { codeField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string NF
    {
        get { return nfField; }
        set { nfField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string Name
    {
        get { return nameField; }
        set { nameField = value; }
    }

    /// <remarks/>
    [XmlAttribute(DataType = "date")]
    public DateTime StartDate
    {
        get { return startDateField; }
        set { startDateField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool StartDateSpecified => StartDate != default;

    /// <remarks/>
    [XmlAttribute(DataType = "date")]
    public DateTime EndDate
    {
        get { return endDateField; }
        set { endDateField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool EndDateSpecified => StartDate != default;
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.fei.org/Result")]
public class ctVenue
{
    private string nameField;

    private string countryField;

    private string fEIIDField;

    private string nFIDField;

    private decimal latitudeField;

    private bool latitudeFieldSpecified;

    private decimal longitudeField;

    private bool longitudeFieldSpecified;

    /// <remarks/>
    [XmlAttribute]
    public string Name
    {
        get { return nameField; }
        set { nameField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string Country
    {
        get { return countryField; }
        set { countryField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string FEIID
    {
        get { return fEIIDField; }
        set { fEIIDField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string NFID
    {
        get { return nFIDField; }
        set { nFIDField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public decimal Latitude
    {
        get { return latitudeField; }
        set { latitudeField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool LatitudeSpecified
    {
        get { return latitudeFieldSpecified; }
        set { latitudeFieldSpecified = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public decimal Longitude
    {
        get { return longitudeField; }
        set { longitudeField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool LongitudeSpecified
    {
        get { return longitudeFieldSpecified; }
        set { longitudeFieldSpecified = value; }
    }
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.fei.org/Result")]
public class ctShowResult
{
    private ctVenue venueField;

    private ctEnduranceEvent[] enduranceEventField;

    private string fEIIDField;

    private string nFIDField;

    private DateTime startDateField;

    private bool startDateFieldSpecified;

    private DateTime endDateField;

    private bool endDateFieldSpecified;

    private string nfField;

    /// <remarks/>
    public ctVenue Venue
    {
        get { return venueField; }
        set { venueField = value; }
    }

    /// <remarks/>
    [XmlElement("EnduranceEvent")]
    public ctEnduranceEvent[] EnduranceEvent
    {
        get { return enduranceEventField; }
        set { enduranceEventField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string FEIID
    {
        get { return fEIIDField; }
        set { fEIIDField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string NFID
    {
        get { return nFIDField; }
        set { nFIDField = value; }
    }

    /// <remarks/>
    [XmlAttribute(DataType = "date")]
    public DateTime StartDate
    {
        get { return startDateField; }
        set { startDateField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool StartDateSpecified => StartDate != default;

    /// <remarks/>
    [XmlAttribute(DataType = "date")]
    public DateTime EndDate
    {
        get { return endDateField; }
        set { endDateField = value; }
    }

    /// <remarks/>
    [XmlIgnore]
    public bool EndDateSpecified => StartDate != default;

    /// <remarks/>
    [XmlAttribute]
    public string NF
    {
        get { return nfField; }
        set { nfField = value; }
    }
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.fei.org/Result")]
public class ctShowResultType
{
    private ctShowResult showField;

    /// <remarks/>
    public ctShowResult Show
    {
        get { return showField; }
        set { showField = value; }
    }
}

/// <remarks/>
[GeneratedCode("xsd", "4.8.3928.0")]
[Serializable]
[DebuggerStepThrough]
[DesignerCategory("code")]
[XmlType(Namespace = "http://www.fei.org/Result")]
public class ctIssuer
{
    private string nameField;

    private string emailField;

    /// <remarks/>
    [XmlAttribute]
    public string Name
    {
        get { return nameField; }
        set { nameField = value; }
    }

    /// <remarks/>
    [XmlAttribute]
    public string Email
    {
        get { return emailField; }
        set { emailField = value; }
    }
}

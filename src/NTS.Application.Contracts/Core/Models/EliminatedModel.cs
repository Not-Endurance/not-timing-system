using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;
using Not.Krud.Abstractions;
using Not.Structures;
using NTS.Application.Contracts.Shared;
using NTS.Application.Contracts.Shared.Models;
using NTS.Domain.Aggregates;
using NTS.Domain.Core.Aggregates;
using NTS.Domain.Core.Aggregates.Participations;
using NTS.Domain.Core.Aggregates.Participations.Entities;
using NTS.Domain.Core.Aggregates.Participations.Objects;
using NTS.Domain.Core.Objects;
using NTS.Domain.Enums;
using NTS.Domain.Objects;

namespace NTS.Application.Contracts.Core.Models;

public class EliminatedModel
{
    public static EliminatedModel MapFrom(Eliminated eliminated)
    {
        if (eliminated is FailedToQualify failedToQualify)
        {
            return new EliminatedModel
            {
                Code = eliminated.Code,
                Reason = eliminated.Complement,
                FtqCodes = failedToQualify.FtqCodes.ToArray(),
            };
        }
        else if (eliminated is Disqualified disqualified)
        {
            return new EliminatedModel
            {
                Code = eliminated.Code,
                Reason = eliminated.Complement,
                DqCodes = disqualified.DqCodes.ToArray(),
            };
        }
        return new EliminatedModel { Code = eliminated.Code, Reason = eliminated.Complement };
    }

    public string Code { get; init; } = default!;
    public string? Reason { get; init; }
    public FailToQualifyCode[]? FtqCodes { get; init; }
    public DisqualifyCode[] DqCodes { get; init; } = default!;

    public Eliminated MapToEntity()
    {
        return Code switch // TODO refactor Eliminated to non-abstract and only FTQ as separate class
        {
            Eliminated.FAILED_TO_QUALIFY => new FailedToQualify(FtqCodes!, Reason),
            Eliminated.WITHDRAWN => new Withdrawn(),
            Eliminated.DISQUALIFIED => new Disqualified(DqCodes, Reason!),
            Eliminated.FINISHED_NOT_RANKED => new FinishedNotRanked(Reason!),
            Eliminated.RETIRED => new Retired(),
            _ => throw new NotImplementedException(),
        };
    }
}

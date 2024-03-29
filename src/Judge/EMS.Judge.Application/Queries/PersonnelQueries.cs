﻿using Core.Domain.State;
using Core.Domain.State.Personnels;
using EMS.Judge.Application.Aggregates.Configurations;
using EMS.Judge.Application.Common;
using System.Collections.Generic;

namespace EMS.Judge.Application.Queries;

public class PersonnelQueries : QueriesBase<Personnel>
{
    private List<Personnel> personnel;

    public PersonnelQueries(IStateContext context) : base(context)
    {
    }

    protected override List<Personnel> Set
        => this.personnel
            ?? (this.personnel = PersonnelAggregator.Aggregate(this.State.Event));
}

﻿using Not.Services;
using NTS.Judge.Blazor.Resources.Localization;
using Microsoft.Extensions.Localization;

namespace NTS.Judge.Blazor.Services;

public class ResxLocalizer : LocalizerBase
{
    private readonly IStringLocalizer<Strings> _stringLocalizer;

    public ResxLocalizer(IStringLocalizer<Strings> stringLocalizer)
    {
        _stringLocalizer = stringLocalizer;
    }

    protected override string GetLocalizedValue(string key)
    {
        return _stringLocalizer[key];
    }
}

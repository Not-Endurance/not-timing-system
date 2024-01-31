﻿using Common.Conventions;

namespace Common.Services;

public abstract class LocalizerBase : ILocalizer
{
	public string Get(params object[] args)
	{
		var localized = args
			.Select(x => this.GetLocalizedValue(x.ToString()!))
			.ToArray();
		return string.Join(string.Empty, localized);
	}

	protected abstract string GetLocalizedValue(string key);
}

public interface ILocalizer : ISingletonService
{
	string Get(params object[] args);
}
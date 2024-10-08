using Not.Injection;

namespace Not.Services;

public abstract class LocalizerBase : ILocalizer
{
	public string Get(params object[] args)
	{
		var localized = args
            .Where(x => x != null)
			.Select(x => this.GetLocalizedValue(x.ToString()!))
			.ToArray();
        if (localized.Length == 0)
        {
            return "";
        }
		return string.Join(" ", localized);
	}

	protected abstract string GetLocalizedValue(string key);
}

public interface ILocalizer : ISingletonService
{
	string Get(params object[] args);
}

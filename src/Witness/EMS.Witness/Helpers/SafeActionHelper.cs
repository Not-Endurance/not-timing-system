namespace EMS.Witness.Helpers;

public static class SafeActionHelper
{
	public static async Task Act(Func<Task> action)
	{
		try
		{
			await action();
		}
		catch (Exception ex)
		{
			await (Application.Current?.MainPage?.DisplayAlert(ex.Message, ex.StackTrace, "Ok") ?? Task.CompletedTask);
		}
	}
}

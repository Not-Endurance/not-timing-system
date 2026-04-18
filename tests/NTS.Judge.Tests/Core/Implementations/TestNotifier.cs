using Not.Notify;

namespace NTS.Judge.Tests.Core.Implementations;

sealed class TestNotifier : INotifier
{
    public List<Exception> Errors { get; } = [];
    public List<string> ErrorMessages { get; } = [];
    public List<string> InformationMessages { get; } = [];
    public List<string> SuccessMessages { get; } = [];
    public List<string> WarningMessages { get; } = [];


    public void Error(string message) 
    {
        ErrorMessages.Add(message);
    }

    public void Error(Exception ex) { Errors.Add(ex); }

    public void Inform(string message) { InformationMessages.Add(message); }

    public void Success(string message) { SuccessMessages.Add(message); }

    public void Warn(string message) { WarningMessages.Add(message); }

    public void Warn(IEnumerable<string> messages)
    {
        WarningMessages.Add(string.Join(Environment.NewLine, messages));
    }
}

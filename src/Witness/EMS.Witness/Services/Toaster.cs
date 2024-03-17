using Core.Application.Services;
using Core.Events;
using EMS.Witness.Shared.Toasts;
using System.Timers;

namespace EMS.Witness.Services;
public class Toaster : IToaster, INotificationService, IDisposable
{
    private readonly List<Toast> toastList = new();
    private readonly System.Timers.Timer timer = new();
    private object lockObject = new();

    public Toaster()
    {
        this.timer.Interval = 1000;
        this.timer.AutoReset = true;
        this.timer.Elapsed += this.HandleTimerElapsed;
        this.timer.Start();
        CoreEvents.ErrorEvent += (sender, exception) =>
        {
            var toast = new Toast(exception.Message, exception.StackTrace, UiColor.Danger, 60);
            this.Add(toast);
        };
    }

    public event EventHandler? ToasterChanged;
    public event EventHandler? ToasterTimerElapsed;
   
    public bool HasToasts => toastList.Count > 0;

    public List<Toast> GetToasts()
    {
        ClearBurntToast();
        return this.toastList.ToList();
    }

    public void Add(string header, string? message, UiColor color, int seconds = 11)
    {
        var toast = new Toast(header, message, color, seconds);
        this.Add(toast);
    }

    public void Add(Toast toast)
    {
        this.toastList.Add(toast);
        if (!this.ClearBurntToast())
        { 
            this.ToasterChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void ClearToast(Toast toast)
    {
        if (this.toastList.Contains(toast))
        {
            this.toastList.Remove(toast);
            if (!this.ClearBurntToast())
            {
                this.ToasterChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public void Dispose()
    {
        if (this.timer is not null)
        {
            this.timer.Elapsed += this.HandleTimerElapsed;
            this.timer.Stop();
        }
    }

    private bool ClearBurntToast()
    {
        var toastsToDelete = new List<Toast>();
        lock (lockObject)
        {
			toastsToDelete = this.toastList.Where(item => item.IsBurnt).ToList();
		}
		if (!toastsToDelete.Any())
        {
            return false;
        }
        
		toastsToDelete.ForEach(toast => this.toastList.Remove(toast));
		this.ToasterChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private void HandleTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        this.ClearBurntToast();
        this.ToasterTimerElapsed?.Invoke(this, EventArgs.Empty);
    }

    public void Error(string message)
    {
        this.Add("Error", message, UiColor.Danger, 10);
    }
}

public interface IToaster
{
    void Add(string header, string? message, UiColor color, int seconds = 10);
	List<Toast> GetToasts();
    void ClearToast(Toast toast);
}

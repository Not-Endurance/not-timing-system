using System.Text;

namespace NTS.Judge.RFID;

public abstract class RfidController
{
    protected const int TAG_READ_START_INDEX = 0;
    protected const int TAG_WRITE_START_INDEX = 4;
    protected const int TAG_DATA_LENGTH = 12;

    public RfidController(TimeSpan? throttle = null)
    {
        Throttle = throttle ?? TimeSpan.FromMilliseconds(100);
    }

    protected abstract string Device { get; }
    public abstract void Connect();
    public abstract void Disconnect();

    protected TimeSpan Throttle { get; }
    public event EventHandler<(DateTime time, string data)>? OnRead;
    public bool IsConnected { get; protected set; }
    public bool IsReading { get; protected set; }
    public bool IsWriting { get; protected set; }

    protected virtual void OnReadEvent((DateTime time, string data) e)
    {
        OnRead?.Invoke(this, e);
    }

    protected virtual byte[] ConvertToByytes(string data)
    {
        return Encoding.UTF8.GetBytes(data);
    }

    protected virtual string ConvertToString(byte[] data)
    {
        return Encoding.UTF8.GetString(data);
    }

    public void RaiseMessage(string message)
    {
        message = $"{Device} {message}";
        Console.WriteLine($"{DateTime.Now}: {message}");
    }

    public void RaiseError(string error)
    {
        var message = $"{Device} ERROR: {error}";
        Console.WriteLine($"{DateTime.Now}: {message}");
    }
}

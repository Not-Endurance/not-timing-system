using Rfid.Entities;
using RfidWriter;
class RfidControllers
{
    static async Task Main(string[] args)
    {
        var argument = Int32.Parse(args[0]);
        var writer = new Writer();
        var tag  = await Task.Run(()=>writer.Write(argument));
        return;
    }
}

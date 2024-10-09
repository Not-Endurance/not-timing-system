using Rfid.Entities;
using Rfid.Hardware;

namespace RfidWriter
{
    public class Writer
    {
        private VupVD67Controller VD67Controller;

        public Writer()
        {
            VD67Controller = new VupVD67Controller(TimeSpan.FromMilliseconds(100));
        }

        public Tag Write(int number)
        {
            var data = VD67Controller.Read();
            var id = data.Substring(9);
            var tag = new Tag(id, number);
            VD67Controller.Write(tag.PrepareToWrite());
            Console.WriteLine(" " + id.Substring(1));
            return tag;
        }
    }

}

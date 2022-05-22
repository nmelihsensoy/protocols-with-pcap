using PcapDotNet.Core;
using PcapDotNet.Packets.IpV4;

namespace http_client
{
    class Program
    {
        private static MandatoryAddresses address;

        private static void Main(string[] args)
        {
            var PcapInit = PcapHelpers.InitInterface(PcapHelpers.InitModes.User_Interaction, PcapHelpers.InitBehavior.Eager, -1);
            address = PcapInit.Item2;
            address.DestIP = new IpV4Address("10.0.0.1");
            Console.WriteLine("Source IP: " + address.SourceIP);
            //PcapInit.Item2.DestIP = new IpV4Address("10.0.0.1");
            Console.WriteLine("Destination IP: " + address.DestIP);
            Console.WriteLine("Source MAC: " + address.SourceMAC);
            Console.WriteLine("Destination MAC: " + address.DestMAC);


            Console.WriteLine("Hello World!");
        }
    }
}

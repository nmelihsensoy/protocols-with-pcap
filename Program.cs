using PcapDotNet.Core;
using PcapDotNet.Packets.IpV4;

namespace http_client
{
    class Program
    {
        private static void Main(string[] args)
        {
            var Pcap = PcapHelpers.InitInterface(PcapHelpers.InitModes.User_Interaction, PcapHelpers.InitBehavior.Eager, -1);
            //var Pcap = PcapHelpers.InitInterface(PcapHelpers.InitModes.Provisioning, PcapHelpers.InitBehavior.Eager, 1);
            Console.WriteLine("Source IP: " + Pcap.Item2.SourceIP);
            //PcapInit.Item2.DestIP = new IpV4Address("10.0.0.1");
            //Console.WriteLine("Destination IP: " + PcapInit.Item2.DestIP);
            Console.WriteLine("Source MAC: " + Pcap.Item2.SourceMAC);
            Console.WriteLine("Destination MAC: " + Pcap.Item2.DestMAC);

            Console.WriteLine("\n");

            string input = args[0];
            IpV4Address DestIP;
            IpV4Address.TryParse(input, out DestIP);
            bool mode = false;
            

            if (DestIP == IpV4Address.Zero)
            {
                HostnameResolver resolver = new HostnameResolver(Pcap.addresses, Pcap.device, "8.8.8.8");
                Console.WriteLine(input);
                DestIP = resolver.Resolve(input);
                mode = true;
            }

            Console.WriteLine("HOST/IP: " + DestIP.ToString());

            //int port = NextPortFinder.GetNextPort();
            //Console.WriteLine("PORT: "+ port);

            MyHttpClient client = new MyHttpClient(Pcap.addresses, Pcap.device);

            if (mode)
            {
                client.GetSync(DestIP, input);
            }
            else
            {
                client.GetSync(DestIP);
            }
            
        }
    }
}

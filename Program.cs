using PcapDotNet.Core;
using PcapDotNet.Packets.IpV4;

namespace http_client
{
    class Program
    {
        private static string DnsServerIp = "8.8.8.8";

        private static void Main(string[] args)
        {
            HostnameResolver resolver;
            HostnameResolver.ResolverStates resolverStatus;
            IpV4Address DestIP;
            MyHttpClient client;
            PcapHelpers.InitModes pcapMode;
            PcapHelpers.InitBehavior pcapBehavior = PcapHelpers.InitBehavior.Eager;
            string inputAddr;
            int provisionedDevId = -1;

            if (args.Length == 0)
            {
                pcapMode = PcapHelpers.InitModes.User_Interaction;
                inputAddr = GetDestinationFromUser();
            }
            else if (args.Length == 1){
                pcapMode = PcapHelpers.InitModes.User_Interaction;
                inputAddr = args[0];
            }
            else if (args.Length == 2){
                pcapMode = PcapHelpers.InitModes.Provisioning;
                provisionedDevId = Int32.Parse(args[0]);
                inputAddr = args[1];
            }
            else{
                return;
            }

            var Pcap = PcapHelpers.InitInterface(pcapMode, pcapBehavior, provisionedDevId);
            //var Pcap = PcapHelpers.InitInterface(PcapHelpers.InitModes.Provisioning, PcapHelpers.InitBehavior.Eager, 1);
            
            PrintDeviceAddresses(Pcap.addresses);

            resolver = new HostnameResolver(Pcap.addresses, Pcap.device, DnsServerIp);
            DestIP = resolver.GetIpFromInput(inputAddr, out resolverStatus);
            
            PrintDestIP(DestIP);

            if (resolverStatus == HostnameResolver.ResolverStates.RESOLVED){
                PrintHostname(inputAddr);
            }

            Console.WriteLine("\n\n");

            client = new MyHttpClient(Pcap.addresses, Pcap.device);

            if (resolverStatus == HostnameResolver.ResolverStates.RESOLVED){
                client.GetSync(DestIP, inputAddr);
            }else{
                client.GetSync(DestIP);
            }

            Console.ReadLine();
        }

        private static void PrintDeviceAddresses(MandatoryAddresses addrs)
        {
            Console.WriteLine("Source IP: " + addrs.SourceIP);
            Console.WriteLine("Source MAC: " + addrs.SourceMAC);
            Console.WriteLine("Destination MAC: " + addrs.DestMAC);
        }

        private static void PrintDestIP(IpV4Address addr)
        {
            Console.WriteLine("Destination IP: " + addr.ToString());
        }

        private static void PrintHostname(string host)
        {
            Console.WriteLine("Host: " + host);
        }

        private static String GetDestinationFromUser()
        {
            Console.WriteLine("Enter address(hostname/ip) to send a http get request.");
            string input = Console.ReadLine()!;
            return input;
        }

    }
}

using System;
using System.Threading;
using System.Collections.Generic;
using PcapDotNet.Core;
using PcapDotNet.Packets;
using PcapDotNet.Packets.Dns;
using PcapDotNet.Packets.Ethernet;
using PcapDotNet.Packets.IpV4;
using PcapDotNet.Packets.Transport;
using PcapDotNet.Core.Extensions;

namespace my_nslookup
{
    class Program
    {
        static MacAddress sourceMAC;
        static MacAddress destinationMAC;
        static string sourceIP_str;
        static IpV4Address sourceIP;
        static IpV4Address destinationIP;
        static LivePacketDevice selectedDevice;
        static DnsDomainName queryDomainName;

        static void Main(string[] args)
        {
            // Retrieve the device list from the local machine
            IList<LivePacketDevice> allDevices = LivePacketDevice.AllLocalMachine;

            if (allDevices.Count == 0)
            {
                Console.WriteLine("No interfaces found! Make sure WinPcap is installed.");
                return;
            }

            if (args[0] == "list")
            {
                // Print the list
                for (int i = 0; i != allDevices.Count; ++i)
                {
                    LivePacketDevice device = allDevices[i];
                    Console.Write((i) + ". " + device.Name);
                    if (device.Description != null)
                        Console.WriteLine(" (" + device.Description + ")");
                    else
                        Console.WriteLine(" (No description available)");
                }
            }

            if (args.Length == 1 && args[0] == "list")
            {
                return;
            }

            int deviceIndex;

            if (args.Length == 0)
            {
                Console.Write("\nPlease enter device number: ");
                deviceIndex = Convert.ToInt32(Console.ReadLine());
            }
            else
            {
                deviceIndex = Int32.Parse(args[0]);
            }

            // Take the selected adapter
            selectedDevice = allDevices[deviceIndex];
            sourceIP_str = GetIpFromDev(ref selectedDevice);

            if (sourceIP_str == null){
                do
                {
                    // Workaround for null ip problem.
                    allDevices = LivePacketDevice.AllLocalMachine;
                    selectedDevice = allDevices[deviceIndex];
                    sourceIP_str = GetIpFromDev(ref selectedDevice);
                } while (sourceIP_str == null);
            }        

            //Parameters
            sourceMAC = LivePacketDeviceExtensions.GetMacAddress(selectedDevice);
            sourceIP = new IpV4Address(sourceIP_str);

            if (args.Length == 4)
            {
                destinationMAC = new MacAddress(args[1]);
                destinationIP = new IpV4Address(args[2]);
                queryDomainName = new DnsDomainName(args[3]);
            }
            else
            {
                destinationMAC = new MacAddress("20:b3:99:55:90:d7"); //ipconfig /all -> Default Gateway -> arp -a
                destinationIP = new IpV4Address("1.1.1.1");
                queryDomainName = new DnsDomainName("duckduckgo.com");
            }

            Thread responseListener = new Thread(ResponseListener);
            Thread querySender = new Thread(DnsQuery);

            responseListener.Start();
            querySender.Start();
        }

        private static String GetIpFromDev(ref LivePacketDevice dev)
        {
            foreach (DeviceAddress address in dev.Addresses)
            {
                Console.WriteLine(address.Address.ToString());
                if (address.Address.Family == SocketAddressFamily.Internet)
                {
                    return address.Address.ToString().Substring(9, address.Address.ToString().Length - 9);
                }
            }
            return null;
        }

        private static void DnsQuery()
        {
            // Open the output device
            using (PacketCommunicator communicator = selectedDevice.Open(100, // name of the device
                                                                         PacketDeviceOpenAttributes.Promiscuous, // promiscuous mode
                                                                         1000)) // read timeout
            {
                // for (ushort i = 0; i < times; i++)
                while (true)
                {
                    var queryPackage = BuildDnsPacket();
                    Console.WriteLine("Press [Enter] to send a new query.");
                    communicator.SendPacket(queryPackage);
                    Console.ReadLine();
                }
            }
        }

        private static Packet BuildDnsPacket()
        {
            EthernetLayer ethernetLayer =
                new EthernetLayer
                {
                    Source = sourceMAC,
                    Destination = destinationMAC,
                    EtherType = EthernetType.None, // Will be filled automatically.
                };

            IpV4Layer ipV4Layer =
                new IpV4Layer
                {
                    Source = sourceIP,
                    CurrentDestination = destinationIP,
                    Fragmentation = IpV4Fragmentation.None,
                    HeaderChecksum = null, // Will be filled automatically.
                    Identification = 123,
                    Options = IpV4Options.None,
                    Protocol = null, // Will be filled automatically.
                    Ttl = 100,
                    TypeOfService = 0,
                };

            UdpLayer udpLayer =
                new UdpLayer
                {
                    SourcePort = 4050,
                    DestinationPort = 53,
                    Checksum = null, // Will be filled automatically.
                    CalculateChecksumValue = true,
                };

            DnsLayer dnsLayer =
                new DnsLayer
                {
                    Id = 100,
                    IsResponse = false,
                    OpCode = DnsOpCode.Query,
                    IsAuthoritativeAnswer = false,
                    IsTruncated = false,
                    IsRecursionDesired = true,
                    IsRecursionAvailable = false,
                    FutureUse = false,
                    IsAuthenticData = false,
                    IsCheckingDisabled = false,
                    ResponseCode = DnsResponseCode.NoError,
                    Queries = new[]
                    {
                    new DnsQueryResourceRecord(queryDomainName,
                                                DnsType.A,
                                                DnsClass.Internet),
                    },
                    Answers = null,
                    Authorities = null,
                    Additionals = null,
                    DomainNameCompressionMode = DnsDomainNameCompressionMode.All,
                };

            PacketBuilder builder = new PacketBuilder(ethernetLayer, ipV4Layer, udpLayer, dnsLayer);

            return builder.Build(DateTime.Now);
        }

        static void ResponseListener()
        {
            // Open the device
            using (PacketCommunicator communicator =
                selectedDevice.Open(65536,                                  // portion of the packet to capture
                                                                            // 65536 guarantees that the whole packet will be captured on all the link layers
                                    PacketDeviceOpenAttributes.Promiscuous, // promiscuous mode
                                    1000))                                  // read timeout
            {
                // Check the link layer. We support only Ethernet for simplicity.
                if (communicator.DataLink.Kind != DataLinkKind.Ethernet)
                {
                    Console.WriteLine("This program works only on Ethernet networks.");
                    return;
                }

                // Compile the filter
                using (BerkeleyPacketFilter filter = communicator.CreateFilter("udp and src host " + destinationIP.ToString() + " and dst host " + sourceIP_str))
                {
                    // Set the filter
                    communicator.SetFilter(filter);
                }

                Console.WriteLine("Listening on " + selectedDevice.Description + "...\n");

                // start the capture
                communicator.ReceivePackets(0, PacketHandler);
            }
        }

        // Callback function invoked by libpcap for every incoming packet
        private static void PacketHandler(Packet packet)
        {
            // print timestamp and length of the packet
            Console.Write(packet.Timestamp.ToString("yyyy-MM-dd hh:mm:ss.fff")); // " length:" + packet.Length

            IpV4Datagram ip = packet.Ethernet.IpV4;
            UdpDatagram udp = ip.Udp;
            DnsDatagram dns = udp.Dns;

            Console.Write("| " + ip.Source + ":" + udp.SourcePort + " -> " + ip.Destination + ":" + udp.DestinationPort + "\n");

            foreach (var value in dns.Answers)
            {
                //Console.WriteLine("##DNS##: " + value.DomainName + " ## "+ (value.Data as DnsResourceDataIpV4).Data.ToString());
                Console.WriteLine("Name:    {0}", value.DomainName);
                Console.WriteLine("Address:  {0}", (value.Data as DnsResourceDataIpV4).Data.ToString());
                Console.WriteLine();
            }
        }
    }
}

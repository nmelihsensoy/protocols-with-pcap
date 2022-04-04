using System;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using PcapDotNet.Core;
using PcapDotNet.Packets;
using PcapDotNet.Packets.Dns;
using PcapDotNet.Packets.Ethernet;
using PcapDotNet.Packets.IpV4;
using PcapDotNet.Packets.Transport;
using PcapDotNet.Core.Extensions;

namespace udp_messaging
{
    class Program
    {
        static MacAddress sourceMAC;
        static MacAddress destinationMAC;
        static string sourceIP_str;
        static IpV4Address sourceIP;
        static IpV4Address destinationIP;
        static LivePacketDevice selectedDevice;
        static string userMessage = "Connected. Initial Message";

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

            if (args.Length == 3)
            {
                destinationMAC = new MacAddress(args[1]);
                destinationIP = new IpV4Address(args[2]);
            }
            else
            {
                destinationMAC = new MacAddress("20:b3:99:55:90:d7"); //ipconfig /all -> Default Gateway -> arp -a
                destinationIP = new IpV4Address("127.0.0.1");
            }

            Thread messageListener = new Thread(MsgListener);
            Thread messageSender = new Thread(MsgSender);

            messageListener.Start();
            messageSender.Start();
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

        private static void MsgSender()
        {
            // Open the output device
            using (PacketCommunicator communicator = selectedDevice.Open(100, // name of the device
                                                                         PacketDeviceOpenAttributes.Promiscuous, // promiscuous mode
                                                                         1000)) // read timeout
            {
                // for (ushort i = 0; i < times; i++)
                while (true)
                {
                    var queryPackage = BuildUdpPacket();
                    communicator.SendPacket(queryPackage); 
                    Console.WriteLine("Press [Enter] to send your message.");
                    userMessage = Console.ReadLine();
                }
            }
        }

        private static void MsgListener()
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
                using (BerkeleyPacketFilter filter = communicator.CreateFilter("udp and src host " + destinationIP.ToString() + " and dst host " + sourceIP_str + " and src port 4050")) 
                {
                    // Set the filter
                    communicator.SetFilter(filter);
                }

                Console.WriteLine("Listening on " + selectedDevice.Description + "...\n");

                // start the capture
                communicator.ReceivePackets(0, PacketHandler);
            }
        }

        private static Packet BuildUdpPacket()
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
                    Source = sourceIP, //1.2.3.4
                    CurrentDestination = destinationIP,//11.22.33.44
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
                    DestinationPort = 25,
                    Checksum = null, // Will be filled automatically.
                    CalculateChecksumValue = true,
                };

            PayloadLayer payloadLayer =
                new PayloadLayer
                {
                    Data = new Datagram(Encoding.ASCII.GetBytes(userMessage)),
                };

            PacketBuilder builder = new PacketBuilder(ethernetLayer, ipV4Layer, udpLayer, payloadLayer);

            return builder.Build(DateTime.Now);
        }

        // Callback function invoked by libpcap for every incoming packet
        private static void PacketHandler(Packet packet)
        {
            // print timestamp and length of the packet
            Console.Write(packet.Timestamp.ToString("yyyy-MM-dd hh:mm:ss.fff")); // " length:" + packet.Length

            IpV4Datagram ip = packet.Ethernet.IpV4;
            UdpDatagram udp = ip.Udp;

            Console.Write("| " + ip.Source + ":" + udp.SourcePort + " -> " + ip.Destination + ":" + udp.DestinationPort + "\n MSG: " + udp.Payload.Decode(Encoding.Default) + "\n\n");
        }
    }
}

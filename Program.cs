using System;
using System.Threading;
using System.Collections.Generic;
using PcapDotNet.Core;
using PcapDotNet.Packets;
using PcapDotNet.Packets.Ethernet;
using PcapDotNet.Packets.IpV4;
using PcapDotNet.Packets.Transport;
using PcapDotNet.Core.Extensions;
using System.IO;

namespace coap_like_protocol
{
    /* 
    VER  TYPE  TOKEN LENGTH  EVALUATION
    01    00        0000     Version1,Comfirmable,Tokenless = 01000000 = 64
    01    01        0000     Version1,Non-confirmable,Tokenless = 01010000 = 80
    01    10        0000     Version1,Acknowledgement,Tokenless = 01100000 = 96
    01    11        0000     Version1,Reset,Tokenless = 01110000 = 112
    */
    enum VER_TYPE_TKL {
        CON = 64,
        NON_CON = 80,
        ACK = 96,
        RST = 112
    }

    enum SUPPORTED_METHODS {
        GET = 32, // 001 00000
        SET = 64, // 010 00000
    }

    enum AVAILABLE_CONTROLS {
        TEMP = 1, // 001 00001, 010 00001
        HUM = 2, // 001 00010, 010 00010
        MOTOR = 3 //001 00011, 010 00011
    }
    
    enum USER_OPERATIONS { 
        READ_TEMP,
        READ_HUM,
        READ_RPM,
        SET_RPM,
        EXIT
    }

    enum PEER_TYPE {
        NOT_SPECIFIED,
        DEVICE,
        REMOTE
    }

    class Program
    {
        static MacAddress sourceMAC;
        static MacAddress destinationMAC;
        static string sourceIP_str;
        static IpV4Address sourceIP;
        static IpV4Address destinationIP;
        static LivePacketDevice selectedDevice;
        static UInt16 msgId=UInt16.MinValue;
        static byte msgFirstHalf;
        static byte msgSecHalf;
        static PEER_TYPE peerType = PEER_TYPE.NOT_SPECIFIED;
        static int mockMotorRpm = 1500;
        static int mockTemp = -1;
        static int mockHumidity = 0;
        static UInt16 destPort = 5683;

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

            do
            {
                // Workaround for null ip problem.
                allDevices = LivePacketDevice.AllLocalMachine;
                selectedDevice = allDevices[deviceIndex];
                sourceIP_str = GetIpFromDev(ref selectedDevice);
            } while (sourceIP_str == null);

            //Parameters
            sourceMAC = LivePacketDeviceExtensions.GetMacAddress(selectedDevice);
            sourceIP = new IpV4Address(sourceIP_str);

            destinationMAC = new MacAddress(args[1]);
            destinationIP = new IpV4Address(args[2]);
            if (args[3] == "device"){
                peerType = PEER_TYPE.DEVICE;
            }else if(args[3] == "remote"){
                peerType = PEER_TYPE.REMOTE;
            }else{
                return;
            }

            Thread senderThread;
            Thread listenerThread = new Thread(packetListener);
            if (peerType == PEER_TYPE.REMOTE){
                senderThread = new Thread(RemoteControl);
                senderThread.Start();
            }

            listenerThread.Start();
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

        private static USER_OPERATIONS UserMenu()
        {
            //Console.Clear();
            Console.WriteLine("Remote Control:");
            Console.WriteLine("0) Read Temperature");
            Console.WriteLine("1) Read Humidity ");
            Console.WriteLine("2) Read Motor Speed ");
            Console.WriteLine("3) Set Motor Speed");
            Console.WriteLine("4) Exit");
            Console.Write("\r\nSelect an option: ");

            return Enum.Parse<USER_OPERATIONS>(Console.ReadLine());
        }
        
        private static void RemoteControl()
        {
            // Open the output device
            using (PacketCommunicator communicator = selectedDevice.Open(100, // name of the device
                                                                         PacketDeviceOpenAttributes.Promiscuous, // promiscuous mode
                                                                         1000)) // read timeout
            {
                USER_OPERATIONS user_input;
                Packet udp;
                do
                {
                    user_input = UserMenu();
                    UInt16 MID = 65535;
                    msgId = MID;
                    msgFirstHalf = (byte)(MID>>8);
                    msgSecHalf = (byte)MID;
                    
                    switch(user_input)
                    {
                        case USER_OPERATIONS.READ_TEMP:
                        {
                            byte[] packet = {(byte)VER_TYPE_TKL.CON, (byte)((uint)AVAILABLE_CONTROLS.TEMP|(uint)SUPPORTED_METHODS.GET), msgFirstHalf, msgSecHalf};
                            udp = BuildUdpPacket(packet);
                            break;
                        }
                        case USER_OPERATIONS.READ_HUM:
                        {
                            byte[] packet = {(byte)VER_TYPE_TKL.CON, (byte)((uint)AVAILABLE_CONTROLS.HUM|(uint)SUPPORTED_METHODS.GET), msgFirstHalf, msgSecHalf};
                            udp = BuildUdpPacket(packet);
                            break;
                        }
                        case USER_OPERATIONS.READ_RPM:
                        {
                            byte[] packet = {(byte)VER_TYPE_TKL.CON, (byte)((uint)AVAILABLE_CONTROLS.MOTOR|(uint)SUPPORTED_METHODS.GET), msgFirstHalf, msgSecHalf};
                            udp = BuildUdpPacket(packet);
                            break;
                        }
                        case USER_OPERATIONS.SET_RPM:
                        {
                            Console.WriteLine("Please enter the RPM value: ");
                            int rpmVal = Int32.Parse(Console.ReadLine());
                            byte[] payload = BitConverter.GetBytes(rpmVal);
                            byte[] packet = {(byte)VER_TYPE_TKL.CON, (byte)((uint)AVAILABLE_CONTROLS.MOTOR|(uint)SUPPORTED_METHODS.SET), msgFirstHalf, msgSecHalf, 255, payload[0], payload[1], payload[2], payload[3]};
                            udp = BuildUdpPacket(packet);
                            break;
                        }
                        default: 
                            udp = null;
                            System.Environment.Exit(0);
                        break;
                    }
                    communicator.SendPacket(udp);

                } while (user_input!= USER_OPERATIONS.EXIT);
            }
        }
        private static Packet BuildUdpPacket(byte[] payload)
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
                    DestinationPort = destPort, //53:DNS, COAP 5683 
                    Checksum = null, // Will be filled automatically.
                    CalculateChecksumValue = true,
                };

            PayloadLayer payloadLayer =
                new PayloadLayer
                {
                    Data = new Datagram(payload),
                };

            PacketBuilder builder = new PacketBuilder(ethernetLayer, ipV4Layer, udpLayer, payloadLayer);

            return builder.Build(DateTime.Now);
        }

        static void packetListener()
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
                using (BerkeleyPacketFilter filter = communicator.CreateFilter("udp dst port 5683"))
                {
                    // Set the filter
                    communicator.SetFilter(filter);
                }

                Console.WriteLine("Listening on " + selectedDevice.Description + "...\n");

                // start the capture
                if (peerType == PEER_TYPE.REMOTE){
                    communicator.ReceivePackets(0, AckPacketHandler);
                }else if(peerType == PEER_TYPE.DEVICE){
                    communicator.ReceivePackets(0, CommandPacketHandler);
                }
                
            }
        }

        // Callback function invoked by libpcap for every incoming packet
        private static void AckPacketHandler(Packet packet)
        {
            Datagram udpPayload = packet.Ethernet.IpV4.Udp.Payload;
            int payloadLength = udpPayload.Length;
            using (MemoryStream ms = udpPayload.ToMemoryStream())
            {
                byte[] rx_payload = new byte[payloadLength];
                ms.Read(rx_payload,0, payloadLength);
                int protocolPayload;

                if (rx_payload[0] == (byte)VER_TYPE_TKL.ACK && rx_payload[2] == msgFirstHalf && rx_payload[3] == msgSecHalf){
                        byte[] incomingNumber = {rx_payload[5], rx_payload[6], rx_payload[7], rx_payload[8]};
                        protocolPayload =  BitConverter.ToInt32(incomingNumber, 0);
                }else{
                    return;
                }

                Console.WriteLine("\nSuccess.Returned : " + protocolPayload);
            }
        }

        private static void SendUdpPacket(Packet pct)
        {
            // Open the output device
            using (PacketCommunicator communicator = selectedDevice.Open(100, // name of the device
                                                                         PacketDeviceOpenAttributes.Promiscuous, // promiscuous mode
                                                                         1000)) // read timeout
            {
                communicator.SendPacket(pct);
            }
        }

        // Callback function invoked by libpcap for every incoming packet
        private static void CommandPacketHandler(Packet packet)
        {
            Datagram udpPayload = packet.Ethernet.IpV4.Udp.Payload;
            int payloadLength = udpPayload.Length;
            Packet udp;
            using (MemoryStream ms = udpPayload.ToMemoryStream())
            {
                byte[] rx_payload = new byte[payloadLength];
                ms.Read(rx_payload,0, payloadLength);
                int protocolPayload = 32767;
                SUPPORTED_METHODS method;
                AVAILABLE_CONTROLS control;

                if (rx_payload[0] == (byte)VER_TYPE_TKL.CON){
                        method = (SUPPORTED_METHODS)(0xE0 & rx_payload[1]);
                        control = (AVAILABLE_CONTROLS)((0x1F & rx_payload[1]));

                        if(method == SUPPORTED_METHODS.SET){
                            byte[] incomingNumber = {rx_payload[5], rx_payload[6], rx_payload[7], rx_payload[8]};
                            protocolPayload =  BitConverter.ToInt32(incomingNumber, 0);
                            if(control == AVAILABLE_CONTROLS.MOTOR){
                                mockMotorRpm = protocolPayload;
                                rx_payload[0] = (byte)VER_TYPE_TKL.ACK;
                                udp = BuildUdpPacket(rx_payload);
                            }else{
                                udp = null;
                                return;
                            }
                        }else if (method == SUPPORTED_METHODS.GET){
                            int sendingNumber;
                            Random rnd = new Random();
                            if (control == AVAILABLE_CONTROLS.TEMP){
                                mockTemp = rnd.Next(20, 25);
                                sendingNumber = mockTemp;
                            }else if(control == AVAILABLE_CONTROLS.HUM){
                                mockHumidity = rnd.Next(50, 70);
                                sendingNumber = mockHumidity;
                            }else if(control == AVAILABLE_CONTROLS.MOTOR){
                                sendingNumber = mockMotorRpm;
                            }else{
                                sendingNumber = 32767;
                                udp = null;
                                return;
                            }
                            byte[] payload = BitConverter.GetBytes(sendingNumber);
                            byte[] responsePacket = {(byte)VER_TYPE_TKL.ACK, rx_payload[1], rx_payload[2], rx_payload[3], 255, payload[0], payload[1], payload[2], payload[3]};
                            udp = BuildUdpPacket(responsePacket);
                        }else{
                            udp = null;
                            return;
                        }
                }else{
                    udp = null;
                    return;
                }
                SendUdpPacket(udp);
            }
        }
    }
}

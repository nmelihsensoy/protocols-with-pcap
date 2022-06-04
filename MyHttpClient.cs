using PcapDotNet.Core;
using PcapDotNet.Packets;
using PcapDotNet.Packets.Ethernet;
using PcapDotNet.Packets.Http;
using PcapDotNet.Packets.IpV4;
using PcapDotNet.Packets.Transport;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace http_client
{
    public class MyHttpClient
    {
        private MandatoryAddresses _addresses;
        private LivePacketDevice _interface;
        private string _host;

        public MyHttpClient(MandatoryAddresses addresses, LivePacketDevice @interface)
        {
            _addresses = addresses;
            _interface = @interface;
            _host = String.Empty;
        }

        public MyHttpClient(MandatoryAddresses addresses, LivePacketDevice @interface, string addr)
        {
            _addresses = addresses;
            _interface = @interface;
            _addresses.DestIP = new IpV4Address(addr);
            _host = String.Empty;
        }

        public void GetSync(string addr)
        {
            _addresses.DestIP = new IpV4Address(addr);
            GetSync();
        }

        public void GetSync(IpV4Address addr)
        {
            _addresses.DestIP = addr;
            GetSync();
        }

        public void GetSync(IpV4Address addr, string host)
        {
            _addresses.DestIP = addr;
            _host = host;
            GetSync();
        }

        public void GetSync()
        {
            uint SEQ = 0;
            uint ACK = 0;
            uint ACK_WAITING = 0;
            ushort REQ_PORT = GetEphemeralPort();
            string ACK_waitingFilter = "tcp and src " + _addresses.DestIP + " and src port 80 and dst port " + REQ_PORT;
            _host = _addresses.DestIP.ToString();
            Packet tmpPacket;

            using (PacketCommunicator communicator = _interface.Open(65536, PacketDeviceOpenAttributes.Promiscuous, 100))
            {
                communicator.SetFilter(ACK_waitingFilter);

                // SEND SYN
                communicator.SendPacket(BuildTcpPacket(REQ_PORT, SEQ, ACK, TcpControlBits.Synchronize));
                ACK_WAITING = ACK + 1;

                // WAIT SYN+ACK
                tmpPacket = WaitForACKPacket(communicator, ACK_WAITING);
                if (tmpPacket.IsValid)
                {
                    SEQ = ACK_WAITING;
                    ACK = tmpPacket.Ethernet.IpV4.Tcp.SequenceNumber + 1;
                }else { /*HANDSHAKE ERROR*/ }

                // SEND ACK
                communicator.SendPacket(BuildTcpPacket(REQ_PORT, SEQ, ACK, TcpControlBits.Acknowledgment));

                // 3-Way Handshake Complete
                // HTTP GET
                tmpPacket = BuildHttpPacket(REQ_PORT, SEQ, ACK, TcpControlBits.Push|TcpControlBits.Acknowledgment, _host, HttpRequestKnownMethod.Get);
                communicator.SendPacket(tmpPacket);
                ACK_WAITING = (uint)(SEQ + tmpPacket.Ethernet.IpV4.Tcp.PayloadLength);

                // WAIT ACK
                tmpPacket = WaitForACKPacket(communicator, ACK_WAITING);

                // WAIT HTTP OK
                tmpPacket = WaitForOK(communicator, ACK_WAITING);
                SEQ = ACK_WAITING;
                ACK = (uint)(tmpPacket.Ethernet.IpV4.Tcp.SequenceNumber + tmpPacket.Ethernet.IpV4.Tcp.PayloadLength);
                PrintContent(tmpPacket.Ethernet.IpV4.Tcp.Http);

                // SEND FIN+ACK
                tmpPacket = BuildTcpPacket(REQ_PORT, SEQ, ACK, TcpControlBits.Fin | TcpControlBits.Acknowledgment);
                communicator.SendPacket(tmpPacket);
                ACK_WAITING = tmpPacket.Ethernet.IpV4.Tcp.SequenceNumber + 1;

                // WAIT FIN+ACK
                tmpPacket = WaitForACKPacket(communicator, ACK_WAITING);

                // ACK
                SEQ = ACK_WAITING;
                ACK = tmpPacket.Ethernet.IpV4.Tcp.SequenceNumber + 1;
                communicator.SendPacket(BuildTcpPacket(REQ_PORT, SEQ, ACK, TcpControlBits.Acknowledgment));
            }
        }

        private static void PrintContent(HttpDatagram content)
        {
            String tmp = content.Decode(Encoding.UTF8);
            Console.WriteLine(tmp);
        }

        private static ushort GetRandomPort()
        {
            return (ushort)(4050 + new Random().Next() % 1000);
        }

        // RFC 6056
        private static ushort GetEphemeralPort()
        {
            return (ushort)NextPortFinder.GetNextPort();
        }

        private static Packet WaitForOK(PacketCommunicator communicator, uint ACK)
        {
            Packet packet;
            while (true)
            {
                if (communicator.ReceivePacket(out packet) == PacketCommunicatorReceiveResult.Ok)
                {
                    if (packet.Ethernet.IpV4.Tcp.AcknowledgmentNumber == ACK && packet.Ethernet.IpV4.Tcp.Http.IsResponse && packet.Ethernet.IpV4.Tcp.Http.IsValid)
                    {
                        break;
                    }

                }
            }
            return packet;
        }

        private static Packet WaitForACKPacket(PacketCommunicator communicator, uint ACK)
        {
            Packet packet;
            while (true)
            {
                if (communicator.ReceivePacket(out packet) == PacketCommunicatorReceiveResult.Ok)
                {
                    if (packet.Ethernet.IpV4.Tcp.AcknowledgmentNumber == ACK)
                    {
                        break;
                    }

                }
            }
            return packet;
        }

        private Packet BuildTcpPacket(ushort tcpPort, uint seqNumber, uint ackNumber, TcpControlBits tcpFlags)
        {
            EthernetLayer ethernetLayer =
                new EthernetLayer
                {
                    Source = _addresses.SourceMAC,
                    Destination = _addresses.DestMAC,
                    EtherType = EthernetType.None, // Will be filled automatically.
                };

            IpV4Layer ipV4Layer =
                new IpV4Layer
                {
                    Source = _addresses.SourceIP,
                    CurrentDestination = _addresses.DestIP,
                    Fragmentation = IpV4Fragmentation.None,
                    HeaderChecksum = null, // Will be filled automatically.
                    Identification = 1234,
                    Options = IpV4Options.None,
                    Protocol = null, // Will be filled automatically.
                    Ttl = 128,
                    TypeOfService = 0,
                };

            TcpLayer tcpLayer =
                new TcpLayer
                {
                    SourcePort = tcpPort,
                    DestinationPort = 80,
                    SequenceNumber = seqNumber,
                    AcknowledgmentNumber = ackNumber,
                    ControlBits = tcpFlags,
                    Window = 8192,
                };

            PacketBuilder builder = new PacketBuilder(ethernetLayer, ipV4Layer, tcpLayer);

            return builder.Build(DateTime.Now);
        }

        private Packet BuildHttpPacket(ushort tcpPort, uint seqNumber, uint ackNumber, TcpControlBits tcpFlags, string host, HttpRequestKnownMethod method )
        {
            EthernetLayer ethernetLayer =
                new EthernetLayer
                {
                    Source = _addresses.SourceMAC,
                    Destination = _addresses.DestMAC,
                    EtherType = EthernetType.None, // Will be filled automatically.
                };

            IpV4Layer ipV4Layer =
                new IpV4Layer
                {
                    Source = _addresses.SourceIP,
                    CurrentDestination = _addresses.DestIP,
                    Fragmentation = IpV4Fragmentation.None,
                    HeaderChecksum = null, // Will be filled automatically.
                    Identification = 1235,
                    Options = IpV4Options.None,
                    Protocol = null, // Will be filled automatically.
                    Ttl = 128,
                    TypeOfService = 0,
                };

            TcpLayer tcpLayer =
                new TcpLayer
                {
                    SourcePort = tcpPort,
                    DestinationPort = 80,
                    SequenceNumber = seqNumber,
                    AcknowledgmentNumber = ackNumber,
                    ControlBits = tcpFlags,
                    Window = 8192,
                };

            HttpRequestLayer httpLayer =
                new HttpRequestLayer
                {
                    Version = HttpVersion.Version11,
                    Header = new HttpHeader(HttpField.CreateField("Host", host)),
                    Method = new HttpRequestMethod(method),
                    Uri = "/",
                };

            PacketBuilder builder = new PacketBuilder(ethernetLayer, ipV4Layer, tcpLayer, httpLayer);

            return builder.Build(DateTime.Now);
        }

    }
}

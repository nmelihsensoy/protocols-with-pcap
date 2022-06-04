using PcapDotNet.Core;
using PcapDotNet.Packets;
using PcapDotNet.Packets.Dns;
using PcapDotNet.Packets.Ethernet;
using PcapDotNet.Packets.IpV4;
using PcapDotNet.Packets.Transport;

namespace http_client
{
    public class HostnameResolver
    {
        public enum ResolverStates
        {
            IP_ALREADY,
            RESOLVED,
            ERROR
        }

        private MandatoryAddresses _addresses;
        private LivePacketDevice _interface;

        public HostnameResolver(MandatoryAddresses addresses, LivePacketDevice @interface)
        {
            _addresses = addresses;
            _interface = @interface;
        }

        public HostnameResolver(MandatoryAddresses addresses, LivePacketDevice @interface, string addr)
        {
            _addresses = addresses;
            _interface = @interface;
            _addresses.DestIP = new IpV4Address(addr);
        }

        public IpV4Address GetIpFromInput(string input)
        {
            ResolverStates tmpState;
            return GetIpFromInput(input, out tmpState);
        }

        public IpV4Address GetIpFromInput(string input, out ResolverStates state)
        {
            IpV4Address DestIP;
            IpV4Address.TryParse(input, out DestIP);

            if (DestIP == IpV4Address.Zero)
            {      
                try
                {
                    DestIP = this.Resolve(input);
                    state = ResolverStates.RESOLVED;
                }
                catch (Exception)
                {
                    DestIP = IpV4Address.Zero;
                    state = ResolverStates.ERROR;
                }
            }
            else
            {
                state = ResolverStates.IP_ALREADY;
            }

            return DestIP;
        }

        public IpV4Address Resolve(string hostname)
        {
            Packet tmpPacket;
            string dnsFilter = "udp and src port 53 and src host " + _addresses.DestIP.ToString() + " and dst host "+_addresses.SourceIP.ToString();
            
            using (PacketCommunicator communicator = _interface.Open(65536, PacketDeviceOpenAttributes.Promiscuous, 100))
            {
                communicator.SetFilter(dnsFilter);
                communicator.SendPacket(BuildDnsPacket(new DnsDomainName(hostname)));

                while (true)
                {
                    if (communicator.ReceivePacket(out tmpPacket) == PacketCommunicatorReceiveResult.Ok)
                    {
                        var answer = tmpPacket.Ethernet.IpV4.Udp.Dns.Answers[0];
                        string domainName = answer.DomainName.ToString();
                        if (domainName == hostname + '.')
                        {
                            break;
                        }
                    }
                }

                DnsDatagram dns = tmpPacket.Ethernet.IpV4.Udp.Dns;
                foreach (var value in dns.Answers)
                {
                    try
                    {
                        return (value.Data as DnsResourceDataIpV4)!.Data;
                    }
                    catch (Exception)
                    {
                        // IpV6
                    }
                }

                return IpV4Address.Zero;
            }
        }

        private Packet BuildDnsPacket(DnsDomainName queryDomainName)
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

    }
}

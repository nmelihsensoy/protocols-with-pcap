using PcapDotNet.Core;
using PcapDotNet.Core.Extensions;
using PcapDotNet.Packets.Ethernet;
using PcapDotNet.Packets.IpV4;
using System.Net;

namespace http_client
{
    public struct MandatoryAddresses
    {
        public IpV4Address SourceIP { get; set; }
        public IpV4Address DestIP { get; set; }
        public MacAddress SourceMAC { get; set; }
        public MacAddress DestMAC { get; set; }
    }

    public class PcapHelpers
    {

        public enum DefaultAddressState
        {
            Success,
            IP_ERROR,
            MAC_ERROR
        }

        public enum InitModes
        {
            User_Interaction,
            Provisioning,
            Informative_Provisioning
        }

        public enum InitBehavior
        {
            FailFast,
            Eager,
            Tolerant
        }

        public static (LivePacketDevice, MandatoryAddresses) InitInterface(InitModes mode, InitBehavior behavior, int provisioning_dev)
        {
            LivePacketDevice? returnDev = null;
            MandatoryAddresses returnAddr = new MandatoryAddresses();
            IList<LivePacketDevice> allDevices = LivePacketDevice.AllLocalMachine;
            DefaultAddressState state;

            switch (mode)
            {
                case InitModes.User_Interaction:
                    returnDev = UserDeviceSelection(ref allDevices, out provisioning_dev);
                    break;
                case InitModes.Provisioning:
                    returnDev = GetInterfaceById(provisioning_dev, ref allDevices);
                    break;
                case InitModes.Informative_Provisioning:
                    PrintDevList(ref allDevices);
                    returnDev = GetInterfaceById(provisioning_dev, ref allDevices);
                    PrintDevInfo(allDevices[provisioning_dev], provisioning_dev);
                    Console.Write(" : Selected.");
                    break;
            }

            if (returnDev == null)
            {
                throw new Exception("Device selection failed.");
            }

            switch (behavior)
            {
                case InitBehavior.FailFast:
                    state = SetDefaultAddresses(ref returnDev, out returnAddr);
                    if (state != DefaultAddressState.Success) throw new Exception("Interface address acquisition failed.");
                    break;
                case InitBehavior.Eager:
                    do
                    {
                        allDevices = LivePacketDevice.AllLocalMachine;
                        returnDev = allDevices[provisioning_dev];
                        state = SetDefaultAddresses(ref returnDev, out returnAddr);
                    } while (state != DefaultAddressState.Success);
                    break;
                case InitBehavior.Tolerant:
                    state = SetDefaultAddresses(ref returnDev, out returnAddr);
                    break;
            }

            return (returnDev, returnAddr);
        }

        public static DefaultAddressState SetDefaultAddresses(ref LivePacketDevice dev, out MandatoryAddresses addr)
        {
            addr = new MandatoryAddresses();
            addr.SourceIP = IpV4Address.Zero;
            addr.DestIP = IpV4Address.Zero;
            addr.SourceMAC = MacAddress.Zero;
            addr.DestMAC = MacAddress.Zero;

            addr.SourceIP = new IpV4Address(GetIpFromDev(ref dev));
            if (addr.SourceIP == IpV4Address.Zero) return DefaultAddressState.IP_ERROR;

            try
            {
                addr.SourceMAC = LivePacketDeviceExtensions.GetMacAddress(dev);
                IPAddress defaultGateway = LivePacketDeviceExtensions.GetNetworkInterface(dev).GetIPProperties().GatewayAddresses[0].Address;
                byte[] destMac = NativeUtils.GetMacAddress(defaultGateway);
                string tmp = BitConverter.ToString(destMac).Replace("-", ":");
                addr.DestMAC = new MacAddress(tmp);

                return DefaultAddressState.Success;
            }
            catch (Exception)
            {
                return DefaultAddressState.MAC_ERROR;
            }
        }

        private static LivePacketDevice GetInterfaceById(int id, ref IList<LivePacketDevice> allDevices)
        {
            if (allDevices.Count == 0) throw new Exception("No interfaces found!");
            if (!CheckUserInput(id, allDevices.Count)) throw new Exception("Wrong interface selection!");

            return allDevices[id];
        }

        private static LivePacketDevice UserDeviceSelection(ref IList<LivePacketDevice> allDevices, out int id)
        {
            id = -1;
            PrintDevList(ref allDevices);
            id = GetUserInput(allDevices.Count);

            return allDevices[id];

        }

        private static void PrintDevList(ref IList<LivePacketDevice> allDevices)
        {
            Console.WriteLine();
            for (int i = 0; i != allDevices.Count; ++i)
            {
                LivePacketDevice device = allDevices[i];
                PrintDevInfo(device, i);
            }
        }

        private static void PrintDevInfo(LivePacketDevice dev, int id)
        {
            Console.Write((id) + ". " + dev.Name);
            if (dev.Description != null)
                Console.WriteLine(" (" + dev.Description + ")");
            else
                Console.WriteLine(" (No description available)");
        }

        private static int GetUserInput(int devCount)
        {
            Console.Write("\nPlease enter device number: ");
            int deviceIndex = Convert.ToInt32(Console.ReadLine());
            if (CheckUserInput(deviceIndex, devCount)) return deviceIndex;
            else return -1;
        }

        private static bool CheckUserInput(int val, int max, int min = 0)
        {
            return (val >= min && val <= max) ? true : false;
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
            return String.Empty;
        }
    }
}

using System.Net;
using System.Runtime.InteropServices;

namespace http_client
{
    public static class NativeUtils
    {
        [DllImport("iphlpapi.dll", ExactSpelling = true)]
        public static extern int SendARP(uint destIP, uint srcIP, byte[] macAddress, ref uint macAddressLength);

        public static byte[] GetMacAddress(IPAddress address)
        {
            byte[] mac = new byte[6];
            uint len = (uint)mac.Length;
            byte[] addressBytes = address.GetAddressBytes();
            var dest = (uint)addressBytes[3] << 24;
            dest += (uint)addressBytes[2] << 16;
            dest += (uint)addressBytes[1] << 8;
            dest += (uint)addressBytes[0];
            if (SendARP(dest, 0, mac, ref len) != 0)
            {
                throw new Exception("The ARP request failed.");
            }
            return mac;
        }
    }
}

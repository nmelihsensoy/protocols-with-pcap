# my-nslookup

This is an example to show how to create a DNS lookup tool with using `Pcap.Net` library for `WinPcap` available devices. Created for educational purposes.

[Udp messaging example](tree/udp-messaging) is also available as a branch. Click  [here](tree/udp-messaging) to check it out.

https://user-images.githubusercontent.com/1637572/160906084-88a6e470-f8e3-4aca-aca6-42fe6c5d5304.mp4

## Build

```
> cd my-nslookup
> dotnet build

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:00.71
```

## Run

1. Change directory into build output folder.

```
cd .\bin\Debug\net5.0\
```

2. Display available network interfaces.

```
> .\my-nslookup.exe list

0. rpcap://\Device\NPF_{C761E1B5-8C1D-4550-99A9-728FF545A3F6} (Network adapter 'Microsoft' on local host)
[...]
```
Keep the corresponding number of the interface that you want to send and listen packets. `0` will be used as `<interface_id>` in forward. 

*Optional. You can skip this.*
Winpcap does not show the hardware name of your interfaces. Therefore you can cross check GUIDs to make sure that you are using the correct interface.

```
> getmac /fo csv /v

"Wi-Fi","Intel(R) Wireless-AC 9260 160MHz","D6-41-13-B3-BC-73","\Device\Tcpip_{C761E1B5-8C1D-4550-99A9-728FF545A3F6}"
```

`{C761E1B5-8C1D-4550-99A9-728FF545A3F6}` make sure part in curly brackets are the same.


3. Find your default gateway's MAC address from your ARP table with the following commands. 

```
> ipconfig /all

[...]
Wireless LAN adapter Wi-Fi:
    
   Default Gateway . . . . . . . . . : 192.168.10.1
   [...]

> arp -a

Interface: 192.168.8.211 --- 0xf
  Internet Address      Physical Address      Type
  192.168.10.1          20-b3-99-55-90-d7     dynamic
  [...]
```

`20:b3:99:55:90:d7` is what we looking for. This address will be used as `<destination_mac>`.

4. Start querying.

```
> .\my-nslookup.exe <interface_id> <destination_mac> <destination_ip> <domain_name>
```
`<destination_ip>` is a DNS Server Ip. `8.8.8.8`, `1.1.1.1` are the popular public services. Could be one of these.
`<domain_name>` is a domain name that you want to learn it's Ip Address. Could be `duckduckgo.com`, `github.com`

## Example Lookup

```
> .\my-nslookup.exe 0 20:b3:99:55:90:d7 8.8.8.8 duckduckgo.com

Internet 192.168.8.211
Listening on Network adapter 'Microsoft' on local host...

Press [Enter] to send a new query.
2022-03-29 12:46:53.245| 8.8.8.8:53 -> 192.168.8.211:4050
Name:    duckduckgo.com.
Address:  40.114.177.156
```

## Dependencies

* [.NET 5.0](https://dotnet.microsoft.com/en-us/download/dotnet/5.0)
* [WinPcap driver](https://www.winpcap.org/).
* [Pcap.Net](https://github.com/PcapDotNet/Pcap.Net) (32bit and 64bit versions included with the repo however 64bit version is used in the project.)

## Credits

Code is heavily based on the [Pcap.Net's Wiki page](https://github.com/PcapDotNet/Pcap.Net/wiki).

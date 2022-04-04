# udp-messaging

This is an example to show how to create a UDP Messaging App using `Pcap.Net` library for `WinPcap` available devices. Created for educational purposes.

[DNS lookup tool](../../) is also available as a branch. Click  [here](../../) to check it out.

https://user-images.githubusercontent.com/1637572/161634147-ea4832b7-e411-48f8-82aa-034421f60993.mp4

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
> .\udp-messaging.exe list

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

4. Start messaging

```
> .\udp-messaging.exe <interface_id> <destination_mac> <destination_ip>
```
`<destination_ip>` is a local ip address of your server or client.

## Example

First Peer

```
> .\udp-messaging.exe 0 20:b3:99:55:90:d7 192.168.8.211

Internet 192.168.8.211
Listening on Network adapter 'Microsoft' on local host...

Press [Enter] to send your message.
2022-04-05 12:06:26.144| 192.168.8.211:4050 -> 192.168.8.211:25
 MSG: Connected. Initial Message
```

Second Peer

```
> .\udp-messaging.exe 0 20:b3:99:55:90:d7 192.168.8.211

Internet 192.168.8.211
Listening on Network adapter 'Microsoft' on local host...

Press [Enter] to send your message.
2022-04-05 12:19:05.099| 192.168.8.211:4050 -> 192.168.8.211:25
 MSG: Connected. Initial Message

2022-04-05 12:19:07.483| 192.168.8.211:4050 -> 192.168.8.211:25
 MSG: Connected. Initial Message
```

## Dependencies

* [.NET 5.0](https://dotnet.microsoft.com/en-us/download/dotnet/5.0)
* [WinPcap driver](https://www.winpcap.org/).
* [Pcap.Net](https://github.com/PcapDotNet/Pcap.Net) (32bit and 64bit versions included with the repo however 64bit version is used in the project.)

## Credits

Code is heavily based on the [Pcap.Net's Wiki page](https://github.com/PcapDotNet/Pcap.Net/wiki).

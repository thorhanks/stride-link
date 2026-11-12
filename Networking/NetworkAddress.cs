using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace StrideLink.Networking;

public static class NetworkAddress
{
    public static IPAddress FindBestLanAddress()
    {
        var candidates = NetworkInterface.GetAllNetworkInterfaces()
            .Where(nic => nic.OperationalStatus == OperationalStatus.Up &&
                          nic.NetworkInterfaceType is not NetworkInterfaceType.Loopback and
                          not NetworkInterfaceType.Tunnel)
            .SelectMany(nic => nic.GetIPProperties().UnicastAddresses
                .Where(address => address.Address.AddressFamily == AddressFamily.InterNetwork)
                .Select(address => new
                {
                    address.Address,
                    HasGateway = nic.GetIPProperties().GatewayAddresses.Any(g =>
                        g.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !g.Address.Equals(IPAddress.Any))
                }))
            .OrderByDescending(item => item.HasGateway)
            .ThenByDescending(item => IsPrivate(item.Address))
            .ToList();

        return candidates.FirstOrDefault()?.Address
            ?? throw new InvalidOperationException(
                "No LAN IPv4 address was found. Connect to Wi-Fi/Ethernet or pass --ip 192.168.x.x.");
    }

    private static bool IsPrivate(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return bytes[0] == 10 ||
               (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
               (bytes[0] == 192 && bytes[1] == 168);
    }
}

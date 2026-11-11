// SPDX-License-Identifier: GPL-2.0-only
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace LibreLine.Desktop.Network;

/// <summary>This PC's addresses, as other players would use them.</summary>
internal static class NetworkAddresses
{
    /// <summary>
    /// This PC's IPv4 addresses on the local network, the one with the default route first. Adapters without a
    /// gateway (virtual machines, WSL, VPN leftovers) come after the real ones.
    /// </summary>
    public static IReadOnlyList<IPAddress> LocalAddresses()
    {
        var primary = PrimaryLocalAddress();
        var found = new List<(IPAddress Address, bool HasGateway)>();
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.OperationalStatus != OperationalStatus.Up
                || adapter.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
            {
                continue;
            }

            var properties = adapter.GetIPProperties();
            var hasGateway = properties.GatewayAddresses.Any(g =>
                g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any));
            foreach (var unicast in properties.UnicastAddresses)
            {
                var address = unicast.Address;
                if (address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address)
                    && !IsLinkLocal(address))
                {
                    found.Add((address, hasGateway));
                }
            }
        }

        return found
            .OrderByDescending(f => f.Address.Equals(primary))
            .ThenByDescending(f => f.HasGateway)
            .Select(f => f.Address)
            .Distinct()
            .ToList();
    }

    /// <summary>The local address that traffic to <paramref name="destination"/> leaves from. Sends nothing.</summary>
    public static IPAddress? LocalAddressFor(IPAddress destination)
    {
        try
        {
            using var socket = new Socket(destination.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(destination, 9);
            return (socket.LocalEndPoint as IPEndPoint)?.Address;
        }
        catch (SocketException)
        {
            return null;
        }
    }

    private static IPAddress? PrimaryLocalAddress() => LocalAddressFor(IPAddress.Parse("192.0.2.1"));

    /// <summary>
    /// True for addresses the internet can't reach directly: private ranges, carrier-grade NAT (100.64.0.0/10) and
    /// link-local. A router whose own external address is one of these sits behind another router or the provider's
    /// NAT, so opening a port on it isn't enough.
    /// </summary>
    public static bool IsPrivate(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != AddressFamily.InterNetwork)
        {
            return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6UniqueLocal
                || IPAddress.IsLoopback(address);
        }

        var b = address.GetAddressBytes();
        return b[0] == 10
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            || (b[0] == 192 && b[1] == 168)
            || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
            || b[0] == 127
            || b[0] == 0
            || IsLinkLocal(address);
    }

    private static bool IsLinkLocal(IPAddress address)
    {
        var b = address.GetAddressBytes();
        return address.AddressFamily == AddressFamily.InterNetwork && b[0] == 169 && b[1] == 254;
    }

    /// <summary>The public address as a web service sees it, for when the router won't say. Null if unknown.</summary>
    public static async Task<IPAddress?> LookUpPublicAddressAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var text = await http.GetStringAsync(new Uri("https://api.ipify.org/"), cancellationToken);
            return IPAddress.TryParse(text.Trim(), out var address) ? address : null;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }

    /// <summary>An address and port as the game's <c>customAsyncServerName</c> setting takes them.</summary>
    public static string ServerName(IPAddress address, int port) =>
        address.AddressFamily == AddressFamily.InterNetworkV6 ? $"[{address}]:{port}" : $"{address}:{port}";
}

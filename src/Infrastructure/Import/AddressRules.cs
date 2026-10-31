using System.Net;
using System.Net.Sockets;

namespace Infrastructure.Import;

// Which addresses the server may fetch from: only the public internet. Private networks, the machine
// itself, link-local (cloud metadata lives at 169.254.169.254), CGNAT, multicast and reserved ranges are refused
public static class AddressRules
{
    private static readonly (IPAddress Network, int Prefix)[] BlockedV4 =
    [
        (IPAddress.Parse("0.0.0.0"), 8),
        (IPAddress.Parse("10.0.0.0"), 8),
        (IPAddress.Parse("100.64.0.0"), 10),
        (IPAddress.Parse("127.0.0.0"), 8),
        (IPAddress.Parse("169.254.0.0"), 16),
        (IPAddress.Parse("172.16.0.0"), 12),
        (IPAddress.Parse("192.0.0.0"), 24),
        (IPAddress.Parse("192.0.2.0"), 24),
        (IPAddress.Parse("192.88.99.0"), 24),
        (IPAddress.Parse("192.168.0.0"), 16),
        (IPAddress.Parse("198.18.0.0"), 15),
        (IPAddress.Parse("198.51.100.0"), 24),
        (IPAddress.Parse("203.0.113.0"), 24),
        (IPAddress.Parse("224.0.0.0"), 4),
        (IPAddress.Parse("240.0.0.0"), 4),
    ];

    private static readonly (IPAddress Network, int Prefix)[] BlockedV6 =
    [
        (IPAddress.Parse("::"), 128),
        (IPAddress.Parse("::1"), 128),
        // NAT64 and 6to4 carry IPv4 addresses that may be private
        (IPAddress.Parse("64:ff9b::"), 96),
        (IPAddress.Parse("64:ff9b:1::"), 48),
        (IPAddress.Parse("2002::"), 16),
        (IPAddress.Parse("100::"), 64),
        (IPAddress.Parse("2001::"), 32),
        (IPAddress.Parse("2001:db8::"), 32),
        // Unique local (fd00::/8 is in it), link-local, site-local and multicast
        (IPAddress.Parse("fc00::"), 7),
        (IPAddress.Parse("fe80::"), 10),
        (IPAddress.Parse("fec0::"), 10),
        (IPAddress.Parse("ff00::"), 8),
    ];

    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();

        var rules = address.AddressFamily switch
        {
            AddressFamily.InterNetwork => BlockedV4,
            AddressFamily.InterNetworkV6 => BlockedV6,
            _ => null,
        };

        return rules is not null && !rules.Any(rule => InRange(address, rule.Network, rule.Prefix));
    }

    private static bool InRange(IPAddress address, IPAddress network, int prefix)
    {
        var bytes = address.GetAddressBytes();
        var networkBytes = network.GetAddressBytes();
        if (bytes.Length != networkBytes.Length) return false;

        var full = prefix / 8;
        for (var i = 0; i < full; i++)
        {
            if (bytes[i] != networkBytes[i]) return false;
        }

        var rest = prefix % 8;
        if (rest == 0) return true;

        var mask = (byte)(0xFF << (8 - rest));
        return (bytes[full] & mask) == (networkBytes[full] & mask);
    }
}

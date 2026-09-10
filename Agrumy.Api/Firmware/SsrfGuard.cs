using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using Agrumy.Shared.Models;

namespace Agrumy.Api.Firmware
{
    /// Blocks HttpFirmwareFetcher/WebhookNotificationChannel from reaching a private/loopback address or a non-https URL, since an admin-configured Custom repository/webhook URL is otherwise trusted outright - a per-consumer allowlist (Pattern = exact hostname or CIDR) relaxes either check independently, and a SocketsHttpHandler.ConnectCallback makes sure the address that gets validated is the exact same one that gets connected to (no second DNS lookup a rebinding attack could change in between).
    public static class SsrfGuard
    {
        public static async Task EnsureAllowedAsync(Uri uri, IReadOnlyList<SsrfAllowlistEntry> allowlist, CancellationToken cancellationToken)
        {
            SsrfAllowlistEntry? hostEntry = FindHostnameEntry(allowlist, uri.Host);
            if (uri.Scheme != Uri.UriSchemeHttps && !SchemeRelaxedByLiteralIp(uri.Host, allowlist) && hostEntry is not { AllowInsecureHttp: true })
            {
                throw new SsrfBlockedException($"'{uri.Scheme}' is not allowed - only https is.");
            }

            await ResolveAndValidateAsync(uri.Host, hostEntry, allowlist, cancellationToken);
        }

        /// The actual DNS-rebinding-proof connect: resolves once, validates the result, then dials that SAME IPAddress directly (bypassing SocketsHttpHandler's own default resolve-then-connect, which would otherwise re-resolve and could get a different, unvalidated answer).
        public static Func<SocketsHttpConnectionContext, CancellationToken, ValueTask<Stream>> CreateConnectCallback(
            Func<CancellationToken, Task<IReadOnlyList<SsrfAllowlistEntry>>> allowlistProvider) =>
            async (context, cancellationToken) =>
            {
                IReadOnlyList<SsrfAllowlistEntry> allowlist = await allowlistProvider(cancellationToken);
                SsrfAllowlistEntry? hostEntry = FindHostnameEntry(allowlist, context.DnsEndPoint.Host);
                IPAddress[] addresses = await ResolveAndValidateAsync(context.DnsEndPoint.Host, hostEntry, allowlist, cancellationToken);

                Exception? lastError = null;
                foreach (IPAddress address in addresses)
                {
                    var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
                    try
                    {
                        await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
                        return new NetworkStream(socket, ownsSocket: true);
                    }
                    catch (Exception ex)
                    {
                        socket.Dispose();
                        lastError = ex;
                    }
                }
                throw new SsrfBlockedException($"could not connect to '{context.DnsEndPoint.Host}'.", lastError ?? new SocketException((int)SocketError.HostUnreachable));
            };

        /// Single resolve, immediately validated - the IPAddress[] this returns is what both EnsureAllowedAsync's caller AND CreateConnectCallback's socket connect are required to use, never a second independent lookup.
        private static async Task<IPAddress[]> ResolveAndValidateAsync(string host, SsrfAllowlistEntry? hostEntry, IReadOnlyList<SsrfAllowlistEntry> allowlist, CancellationToken cancellationToken)
        {
            IPAddress[] addresses;
            try
            {
                addresses = IPAddress.TryParse(host, out IPAddress? literal) ? [literal] : await Dns.GetHostAddressesAsync(host, cancellationToken);
            }
            catch (Exception ex) when (ex is SocketException or ArgumentException)
            {
                throw new SsrfBlockedException($"could not resolve host '{host}'.", ex);
            }

            // Every resolved address is checked, not just the first - DNS can return a public address alongside a private one, and a later connect attempt isn't guaranteed to pick either one.
            if (addresses.Length == 0 || Array.Exists(addresses, a => IsPrivateOrReserved(a) && hostEntry is not { AllowPrivateNetwork: true } && !MatchingCidrEntries(allowlist, a).Any(e => e.AllowPrivateNetwork)))
            {
                throw new SsrfBlockedException($"'{host}' resolves to a private/reserved address.");
            }
            return addresses;
        }

        /// A CIDR-form allowlist entry (e.g. "192.168.1.0/24") can only relax the https-only check pre-resolve when the URL's host is ITSELF a literal IP - a domain name needs an actual lookup to know its address, which would defeat EnsureAllowedAsync's "reject non-https without ever touching DNS" fast path (see SsrfGuardTests).
        private static bool SchemeRelaxedByLiteralIp(string host, IReadOnlyList<SsrfAllowlistEntry> allowlist) =>
            IPAddress.TryParse(host, out IPAddress? literal) && MatchingCidrEntries(allowlist, literal).Any(e => e.AllowInsecureHttp);

        private static SsrfAllowlistEntry? FindHostnameEntry(IReadOnlyList<SsrfAllowlistEntry> allowlist, string host) =>
            allowlist.FirstOrDefault(e => !IsCidrPattern(e.Pattern) && string.Equals(e.Pattern, host, StringComparison.OrdinalIgnoreCase));

        private static IEnumerable<SsrfAllowlistEntry> MatchingCidrEntries(IReadOnlyList<SsrfAllowlistEntry> allowlist, IPAddress address) =>
            allowlist.Where(e => IPNetwork.TryParse(e.Pattern, out IPNetwork network) && network.Contains(address));

        internal static bool IsCidrPattern(string pattern) => IPNetwork.TryParse(pattern, out _);

        /// RFC 1918/5735/4193 etc. ranges plus loopback/link-local/multicast - checked against the resolved IP, since a public-looking hostname can still resolve to one of these (DNS rebinding, or a misconfigured record).
        internal static bool IsPrivateOrReserved(IPAddress address)
        {
            if (address.IsIPv4MappedToIPv6)
            {
                address = address.MapToIPv4();
            }
            if (IPAddress.IsLoopback(address))
            {
                return true;
            }

            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                byte[] b = address.GetAddressBytes();
                return b[0] == 10                                          // 10.0.0.0/8
                    || (b[0] == 172 && b[1] is >= 16 and <= 31)             // 172.16.0.0/12
                    || (b[0] == 192 && b[1] == 168)                        // 192.168.0.0/16
                    || (b[0] == 169 && b[1] == 254)                        // 169.254.0.0/16 link-local
                    || b[0] == 0                                           // 0.0.0.0/8
                    || (b[0] == 100 && b[1] is >= 64 and <= 127)           // 100.64.0.0/10 CGNAT
                    || (b[0] == 192 && b[1] == 0 && b[2] == 0)             // 192.0.0.0/24 IETF protocol assignments
                    || (b[0] == 192 && b[1] == 0 && b[2] == 2)             // 192.0.2.0/24 TEST-NET-1
                    || (b[0] == 198 && b[1] is 18 or 19)                   // 198.18.0.0/15 benchmarking
                    || (b[0] == 198 && b[1] == 51 && b[2] == 100)          // 198.51.100.0/24 TEST-NET-2
                    || (b[0] == 203 && b[1] == 0 && b[2] == 113)           // 203.0.113.0/24 TEST-NET-3
                    || b[0] >= 224;                                        // 224.0.0.0/4 multicast + 240.0.0.0/4 reserved
            }

            if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast)
                {
                    return true;
                }
                byte[] b = address.GetAddressBytes();
                return (b[0] & 0xFE) == 0xFC; // fc00::/7 unique local
            }

            return false;
        }
    }

    /// Thrown by SsrfGuard, distinct from HttpRequestException, so FirmwareApiController can tell "URL rejected as unsafe" apart from "remote server errored" and surface an admin-actionable message.
    public sealed class SsrfBlockedException : Exception
    {
        public SsrfBlockedException(string reason) : base($"Blocked outbound request: {reason}") { }
        public SsrfBlockedException(string reason, Exception innerException) : base($"Blocked outbound request: {reason}", innerException) { }
    }
}

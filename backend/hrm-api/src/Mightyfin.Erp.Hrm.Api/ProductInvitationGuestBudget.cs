using System.Net;
using System.Security.Cryptography;

namespace Mightyfin.Erp.Hrm.Api;

/// <summary>Fixed memory and no queue. Connection addresses are never retained or logged.</summary>
internal sealed class ProductInvitationGuestBudget(TimeProvider clock)
{
    internal enum Operation { Session, Prepare, Start }
    internal sealed record Policy(Operation Value);
    private const int Buckets = 256;
    private readonly object sync = new();
    private readonly byte[] salt = RandomNumberGenerator.GetBytes(32);
    private readonly int[,] counts = new int[3, Buckets];
    private readonly int[] totals = new int[3];
    private readonly int[] perBucket = [60, 10, 20];
    private readonly int[] perProcess = [600, 100, 200];
    private long window = clock.GetTimestamp();

    internal bool TryAcquire(Operation operation, IPAddress? address)
    {
        var bytes = address?.MapToIPv6().GetAddressBytes() ?? new byte[16];
        // IPv6 privacy-address rotation within one /64 must not bypass the limit.
        if (address is not null && !address.IsIPv4MappedToIPv6
            && address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            Array.Clear(bytes, 8, 8);
        var bucket = HMACSHA256.HashData(salt, bytes)[0];
        var index = (int)operation;
        lock (sync)
        {
            var now = clock.GetTimestamp();
            if (clock.GetElapsedTime(window, now) >= TimeSpan.FromMinutes(1))
            {
                Array.Clear(counts); Array.Clear(totals); window = now;
            }
            if (counts[index, bucket] >= perBucket[index] || totals[index] >= perProcess[index]) return false;
            counts[index, bucket]++; totals[index]++;
            return true;
        }
    }
}

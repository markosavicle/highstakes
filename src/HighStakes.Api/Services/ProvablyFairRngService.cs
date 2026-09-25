using System.Security.Cryptography;
using System.Text;

namespace HighStakes.Api.Services;

public sealed class ProvablyFairRngService : IProvablyFairRngService
{
    public string GenerateServerSeed()
    {
        // 32 cryptographically secure random bytes, hex-encoded.
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToHexString(bytes);
    }

    public string HashServerSeed(string serverSeed)
    {
        var bytes = Encoding.UTF8.GetBytes(serverSeed);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    public int RollNumber(string serverSeed, string clientSeed, long nonce, int max)
    {
        if (max <= 0) throw new ArgumentOutOfRangeException(nameof(max));

        var key = Encoding.UTF8.GetBytes(serverSeed);
        var message = Encoding.UTF8.GetBytes($"{clientSeed}:{nonce}");

        using var hmac = new HMACSHA256(key);
        var hash = hmac.ComputeHash(message);

        // Use the first 4 bytes as an unsigned 32-bit integer, then reduce mod max.
        var value = BitConverter.ToUInt32(hash, 0);
        return (int)(value % (uint)max);
    }
}

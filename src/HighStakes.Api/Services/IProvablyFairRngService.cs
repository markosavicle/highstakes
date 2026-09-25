namespace HighStakes.Api.Services;

public interface IProvablyFairRngService
{
    /// Generates a new secret server seed for a round (kept hidden until reveal).
    string GenerateServerSeed();

    /// SHA-256 commitment of the server seed, safe to publish before the round resolves.
    string HashServerSeed(string serverSeed);

    /// Deterministically derives a number in [0, max) from serverSeed + clientSeed + nonce
    /// using HMAC-SHA256. Anyone can recompute this once serverSeed is revealed.
    int RollNumber(string serverSeed, string clientSeed, long nonce, int max);
}

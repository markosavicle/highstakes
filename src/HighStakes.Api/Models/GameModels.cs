namespace HighStakes.Api.Models;

public enum TablePhase
{
    Betting,
    Rolling,
    Results
}

public sealed record Bet(string PlayerId, decimal Amount, string Choice);

public sealed record PlayerOutcome(
    string PlayerId,
    decimal BetAmount,
    string Choice,
    bool Won,
    decimal Payout,
    decimal NewBalance);

public sealed class RoundResult
{
    public required int RoundNumber { get; init; }
    public required int Roll { get; init; }
    public required string Winner { get; init; } // "high" or "low"
    public required string ServerSeedHash { get; init; }
    public required string RevealedServerSeed { get; init; }
    public required string ClientSeed { get; init; }
    public required long Nonce { get; init; }
    public required List<PlayerOutcome> Outcomes { get; init; }
}

public sealed class TableState
{
    public required string Id { get; init; }
    public TablePhase Phase { get; set; } = TablePhase.Betting;
    public DateTime PhaseEndsUtc { get; set; }
    public int RoundNumber { get; set; } = 0;

    // Commit-reveal provably-fair state for the CURRENT round
    public string CurrentServerSeed { get; set; } = string.Empty;
    public string CurrentServerSeedHash { get; set; } = string.Empty;
    public string CurrentClientSeed { get; set; } = string.Empty;

    public System.Collections.Concurrent.ConcurrentDictionary<string, Bet> Bets { get; } = new();
    public System.Collections.Concurrent.ConcurrentDictionary<string, byte> ConnectedPlayerIds { get; } = new();

    public RoundResult? LastResult { get; set; }
}

using System.ComponentModel.DataAnnotations;

namespace HighStakes.Api.Models;

public class PlayerEntity
{
    [Key]
    public required string Id { get; set; }
    public required string PasswordHash { get; set; }
    public decimal Balance { get; set; } = 1000m;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class GameRoundEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string TableId { get; set; }
    public int RoundNumber { get; set; }
    public int Roll { get; set; }
    public required string Winner { get; set; }
    public required string ServerSeedHash { get; set; }
    public required string RevealedServerSeed { get; set; }
    public required string ClientSeed { get; set; }
    public long Nonce { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

// Renamed conceptually to "round activity" — this is a play-money ledger for the
// demo's in-game economy only (bets/payouts/resets), never a real financial record.
public class RoundActivityEntity
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string PlayerId { get; set; }
    public required string Type { get; set; } // "STARTING_BALANCE", "BET", "WIN", "RESET"
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

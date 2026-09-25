using System.Security.Cryptography;
using HighStakes.Api.Models;

namespace HighStakes.Api.Services;

/// Play-money-only wallet. No real-money deposit/withdrawal exists anywhere in
/// this service by design — this is a portfolio demo, not a payment system.
public sealed class WalletService : IWalletService
{
    private const decimal StartingBalance = 1000m;
    private const int Pbkdf2Iterations = 100_000;
    private const int SaltSizeBytes = 16;
    private const int HashSizeBytes = 32;

    private readonly IServiceProvider _serviceProvider;

    public WalletService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, HashSizeBytes);
        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string storedHash)
    {
        var parts = storedHash.Split(':');
        if (parts.Length != 2) return false;

        var salt = Convert.FromBase64String(parts[0]);
        var expectedHash = Convert.FromBase64String(parts[1]);

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            password, salt, Pbkdf2Iterations, HashAlgorithmName.SHA256, HashSizeBytes);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private void LogActivity(HighStakesDbContext db, string playerId, string type, decimal amount, decimal balanceAfter)
    {
        db.RoundActivity.Add(new RoundActivityEntity
        {
            PlayerId = playerId,
            Type = type,
            Amount = amount,
            BalanceAfter = balanceAfter
        });
    }

    public bool Register(string username, string password, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(username) || username.Length < 3)
        {
            error = "Username must be at least 3 characters.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(password) || password.Length < 4)
        {
            error = "Password must be at least 4 characters.";
            return false;
        }

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HighStakesDbContext>();

        var existing = db.Players.FirstOrDefault(p => p.Id.ToLower() == username.ToLower());
        if (existing != null)
        {
            error = "Username is already taken.";
            return false;
        }

        var player = new PlayerEntity
        {
            Id = username,
            PasswordHash = HashPassword(password),
            Balance = StartingBalance
        };

        db.Players.Add(player);
        LogActivity(db, username, "STARTING_BALANCE", StartingBalance, StartingBalance);
        db.SaveChanges();
        return true;
    }

    public bool ValidateCredentials(string username, string password)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HighStakesDbContext>();

        var player = db.Players.FirstOrDefault(p => p.Id.ToLower() == username.ToLower());
        return player != null && VerifyPassword(password, player.PasswordHash);
    }

    public decimal GetBalance(string playerId)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HighStakesDbContext>();

        var player = db.Players.FirstOrDefault(p => p.Id == playerId);
        return player?.Balance ?? 0m;
    }

    public bool TryDebit(string playerId, decimal amount, string activityType = "BET")
    {
        if (amount <= 0) return false;

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HighStakesDbContext>();

        var player = db.Players.FirstOrDefault(p => p.Id == playerId);
        if (player == null || player.Balance < amount) return false;

        player.Balance -= amount;
        LogActivity(db, playerId, activityType, -amount, player.Balance);
        db.SaveChanges();
        return true;
    }

    public void Credit(string playerId, decimal amount, string activityType = "WIN")
    {
        if (amount <= 0) return;

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HighStakesDbContext>();

        var player = db.Players.FirstOrDefault(p => p.Id == playerId);
        if (player != null)
        {
            player.Balance += amount;
            LogActivity(db, playerId, activityType, amount, player.Balance);
            db.SaveChanges();
        }
    }

    /// Resets a player's play-money balance back to the starting amount.
    /// This is the ONLY way balance increases outside of winning a round —
    /// there is deliberately no deposit/top-up path tied to real money.
    public void ResetBalance(string playerId)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HighStakesDbContext>();

        var player = db.Players.FirstOrDefault(p => p.Id == playerId);
        if (player != null)
        {
            player.Balance = StartingBalance;
            LogActivity(db, playerId, "RESET", StartingBalance, StartingBalance);
            db.SaveChanges();
        }
    }

    public bool ChangePassword(string username, string newPassword, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 4)
        {
            error = "New password must be at least 4 characters.";
            return false;
        }

        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<HighStakesDbContext>();

        var player = db.Players.FirstOrDefault(p => p.Id.ToLower() == username.ToLower());
        if (player == null)
        {
            error = "User not found.";
            return false;
        }

        player.PasswordHash = HashPassword(newPassword);
        db.SaveChanges();
        return true;
    }

}

using System.Collections.Concurrent;
using HighStakes.Api.Models;

namespace HighStakes.Api.Services;

public sealed class TableManager : ITableManager
{
    private readonly ConcurrentDictionary<string, TableState> _tables = new();
    private readonly IWalletService _wallet;

    private const decimal MinBet = 1m;
    private const decimal MaxBet = 500m;

    public TableManager(IWalletService wallet)
    {
        _wallet = wallet;
    }

    public TableState GetOrCreateTable(string tableId)
    {
        return _tables.GetOrAdd(tableId, id => new TableState
        {
            Id = id,
            PhaseEndsUtc = DateTime.UtcNow.AddSeconds(GameLoopService.BettingDurationSeconds)
        });
    }

    public IEnumerable<string> GetAllTableIds() => _tables.Keys;

    public bool TryPlaceBet(string tableId, string playerId, decimal amount, string choice, out string? error)
    {
        error = null;
        var table = GetOrCreateTable(tableId);

        if (table.Phase != TablePhase.Betting)
        {
            error = "Betting is closed for this round.";
            return false;
        }

        choice = choice.Trim().ToLowerInvariant();
        if (choice is not ("high" or "low"))
        {
            error = "Choice must be 'high' or 'low'.";
            return false;
        }

        if (amount < MinBet || amount > MaxBet)
        {
            error = $"Bet must be between {MinBet} and {MaxBet}.";
            return false;
        }

        if (table.Bets.ContainsKey(playerId))
        {
            error = "You already placed a bet this round.";
            return false;
        }

        if (!_wallet.TryDebit(playerId, amount, "BET"))
        {
            error = "Insufficient balance.";
            return false;
        }

        table.Bets[playerId] = new Bet(playerId, amount, choice);
        return true;
    }

    public void RemovePlayer(string tableId, string playerId)
    {
        if (_tables.TryGetValue(tableId, out var table))
        {
            table.Bets.TryRemove(playerId, out _);
        }
    }
}

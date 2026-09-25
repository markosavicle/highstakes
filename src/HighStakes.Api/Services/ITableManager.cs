using HighStakes.Api.Models;

namespace HighStakes.Api.Services;

public interface ITableManager
{
    TableState GetOrCreateTable(string tableId);
    IEnumerable<string> GetAllTableIds();
    bool TryPlaceBet(string tableId, string playerId, decimal amount, string choice, out string? error);
    void RemovePlayer(string tableId, string playerId);
}

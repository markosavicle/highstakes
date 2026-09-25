using HighStakes.Api.Models;
using HighStakes.Api.Services;
using Microsoft.AspNetCore.SignalR;

namespace HighStakes.Api.Hubs;

public sealed class GameHub : Hub
{
    private readonly ITableManager _tables;
    private readonly IWalletService _wallet;
    private readonly ILogger<GameHub> _logger;

    public GameHub(ITableManager tables, IWalletService wallet, ILogger<GameHub> logger)
    {
        _tables = tables;
        _wallet = wallet;
        _logger = logger;
    }

    private static string GroupName(string tableId) => $"table-{tableId}";

    public async Task JoinTable(string tableId, string username)
    {
        var playerId = string.IsNullOrWhiteSpace(username) ? Context.ConnectionId : username.Trim();

        var table = _tables.GetOrCreateTable(tableId);
        table.ConnectedPlayerIds[playerId] = 0;

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(tableId));

        var balance = _wallet.GetBalance(playerId);

        await Clients.Caller.SendAsync("Welcome", new
        {
            playerId,
            balance,
            snapshot = BuildSnapshot(table)
        });

        await Clients.OthersInGroup(GroupName(tableId)).SendAsync("PlayerCountUpdated", table.ConnectedPlayerIds.Count);

        _logger.LogInformation("Player {PlayerId} joined table {TableId}", playerId, tableId);
    }

    public async Task PlaceBet(string tableId, string username, decimal amount, string choice)
    {
        var playerId = string.IsNullOrWhiteSpace(username) ? Context.ConnectionId : username.Trim();

        if (_tables.TryPlaceBet(tableId, playerId, amount, choice, out var error))
        {
            await Clients.Group(GroupName(tableId)).SendAsync("BetPlaced",
                new BetDto(playerId, amount, choice.ToLowerInvariant()));

            await Clients.Caller.SendAsync("BalanceUpdated", _wallet.GetBalance(playerId));
        }
        else
        {
            await Clients.Caller.SendAsync("Error", new ErrorDto(error ?? "Bet rejected."));
        }
    }

    public async Task LeaveTable(string tableId, string username)
    {
        var playerId = string.IsNullOrWhiteSpace(username) ? Context.ConnectionId : username.Trim();
        var table = _tables.GetOrCreateTable(tableId);
        table.ConnectedPlayerIds.TryRemove(playerId, out _);
        _tables.RemovePlayer(tableId, playerId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(tableId));
        await Clients.Group(GroupName(tableId)).SendAsync("PlayerCountUpdated", table.ConnectedPlayerIds.Count);
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        foreach (var tableId in _tables.GetAllTableIds())
        {
            _tables.RemovePlayer(tableId, Context.ConnectionId);
        }
        return base.OnDisconnectedAsync(exception);
    }

    internal static object BuildSnapshot(TableState table) => new TableSnapshotDto(
        TableId: table.Id,
        Phase: table.Phase.ToString(),
        PhaseEndsUtc: table.PhaseEndsUtc,
        RoundNumber: table.RoundNumber,
        CurrentServerSeedHash: table.CurrentServerSeedHash,
        PlayerCount: table.ConnectedPlayerIds.Count,
        ActiveBets: table.Bets.Values.Select(b => new BetDto(b.PlayerId, b.Amount, b.Choice)),
        LastResult: table.LastResult is null ? null : new RoundResultDto(
            table.LastResult.RoundNumber,
            table.LastResult.Roll,
            table.LastResult.Winner,
            table.LastResult.ServerSeedHash,
            table.LastResult.RevealedServerSeed,
            table.LastResult.ClientSeed,
            table.LastResult.Nonce,
            table.LastResult.Outcomes)
    );
}

using HighStakes.Api.Hubs;
using HighStakes.Api.Models;
using Microsoft.AspNetCore.SignalR;

namespace HighStakes.Api.Services;

public sealed class GameLoopService : BackgroundService
{
    public const int BettingDurationSeconds = 8;
    public const int ResultsDurationSeconds = 5;
    private const int RollRange = 100; // 0..99
    private const decimal PayoutMultiplier = 1.94m; // ~3% house edge on an even-money bet

    private readonly ITableManager _tables;
    private readonly IProvablyFairRngService _rng;
    private readonly IWalletService _wallet;
    private readonly IHubContext<GameHub> _hub;
    private readonly ILogger<GameLoopService> _logger;

    public GameLoopService(
        ITableManager tables,
        IProvablyFairRngService rng,
        IWalletService wallet,
        IHubContext<GameHub> hub,
        ILogger<GameLoopService> logger)
    {
        _tables = tables;
        _rng = rng;
        _wallet = wallet;
        _hub = hub;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Seed a default table so there's something to connect to immediately.
        _tables.GetOrCreateTable("main");
        await StartNewRound(_tables.GetOrCreateTable("main"));

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var tableId in _tables.GetAllTableIds())
            {
                var table = _tables.GetOrCreateTable(tableId);

                if (DateTime.UtcNow < table.PhaseEndsUtc)
                    continue;

                switch (table.Phase)
                {
                    case TablePhase.Betting:
                        await ResolveRound(table);
                        break;

                    case TablePhase.Results:
                        await StartNewRound(table);
                        break;
                }
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // shutting down
            }
        }
    }

    private async Task StartNewRound(TableState table)
    {
        table.RoundNumber++;
        table.Bets.Clear();
        table.LastResult = null;

        table.CurrentServerSeed = _rng.GenerateServerSeed();
        table.CurrentServerSeedHash = _rng.HashServerSeed(table.CurrentServerSeed);
        table.CurrentClientSeed = $"table-{table.Id}"; // Phase 2 TODO: let players contribute this

        table.Phase = TablePhase.Betting;
        table.PhaseEndsUtc = DateTime.UtcNow.AddSeconds(BettingDurationSeconds);

        await _hub.Clients.Group($"table-{table.Id}").SendAsync("NewRound", new
        {
            table.RoundNumber,
            table.CurrentServerSeedHash,
            BettingClosesUtc = table.PhaseEndsUtc
        });

        _logger.LogInformation("Table {TableId} round {Round} betting open (seed hash {Hash})",
            table.Id, table.RoundNumber, table.CurrentServerSeedHash);
    }

    private async Task ResolveRound(TableState table)
    {
        table.Phase = TablePhase.Rolling;

        var roll = _rng.RollNumber(
            table.CurrentServerSeed,
            table.CurrentClientSeed,
            nonce: table.RoundNumber,
            max: RollRange);

        var winner = roll >= 50 ? "high" : "low";

        var outcomes = new List<PlayerOutcome>();
        foreach (var bet in table.Bets.Values)
        {
            var won = bet.Choice == winner;
            var payout = won ? Math.Round(bet.Amount * PayoutMultiplier, 2) : 0m;

            if (won)
            {
                _wallet.Credit(bet.PlayerId, payout, "WIN");
            }

            outcomes.Add(new PlayerOutcome(
                bet.PlayerId, bet.Amount, bet.Choice, won, payout, _wallet.GetBalance(bet.PlayerId)));
        }

        table.LastResult = new RoundResult
        {
            RoundNumber = table.RoundNumber,
            Roll = roll,
            Winner = winner,
            ServerSeedHash = table.CurrentServerSeedHash,
            RevealedServerSeed = table.CurrentServerSeed, // reveal AFTER resolving, never before
            ClientSeed = table.CurrentClientSeed,
            Nonce = table.RoundNumber,
            Outcomes = outcomes
        };

        table.Phase = TablePhase.Results;
        table.PhaseEndsUtc = DateTime.UtcNow.AddSeconds(ResultsDurationSeconds);

        await _hub.Clients.Group($"table-{table.Id}").SendAsync("RoundResult", new RoundResultDto(
            table.LastResult.RoundNumber,
            table.LastResult.Roll,
            table.LastResult.Winner,
            table.LastResult.ServerSeedHash,
            table.LastResult.RevealedServerSeed,
            table.LastResult.ClientSeed,
            table.LastResult.Nonce,
            table.LastResult.Outcomes));

        _logger.LogInformation("Table {TableId} round {Round} resolved: roll={Roll} winner={Winner}",
            table.Id, table.RoundNumber, roll, winner);
    }
}

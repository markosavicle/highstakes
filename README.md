# HighStakes

A real-time multiplayer **High/Low betting game server** written in **C# / ASP.NET Core (.NET 9)**, with **SignalR** for live updates and a **provably fair** RNG. Play-money only.

Players join a shared table, bet whether the next roll (0–99) will be *high* or *low*, and every connected client sees the round progress in real time. After each round the server reveals its secret seed so anyone can verify the result was not manipulated.

## Features

- **Server-authoritative game loop** — a `BackgroundService` drives each table through `Betting → Results` phases on a timer (8 s betting, 5 s results) and resolves rounds independently of client connections.
- **Real-time multiplayer** — a SignalR hub (`/hubs/game`) handles joining/leaving tables and placing bets, and broadcasts table snapshots and round results to everyone at the table.
- **Provably fair RNG (commit–reveal)**
  1. Before betting opens, the server generates a secret seed with a CSPRNG and publishes only its **SHA-256 hash**.
  2. The roll is derived deterministically with **HMAC-SHA256(serverSeed, clientSeed:nonce)**.
  3. After the round, the seed is revealed, so anyone can recompute the hash and the roll.
- **Wallet and accounts** — registration/login with **PBKDF2-hashed passwords** (salted, constant-time comparison), balance debits/credits and a round activity history, persisted with **EF Core + SQLite**.
- **House edge** — even-money bets pay 1.94×, roughly a 3% edge.
- **Thread-safe table state** using `ConcurrentDictionary` for bets and connected players.

## Architecture

```
src/HighStakes.Api/
├── Program.cs                 # DI setup, minimal API endpoints, SignalR hub mapping
├── Hubs/GameHub.cs            # Real-time API: JoinTable, PlaceBet, LeaveTable
├── Models/                    # Entities, DTOs, game state (TableState, RoundResult, ...)
└── Services/
    ├── GameLoopService.cs     # Hosted service running the round state machine
    ├── TableManager.cs        # Table lifecycle and bet validation
    ├── WalletService.cs       # Accounts, password hashing, balances, history
    ├── ProvablyFairRngService.cs
    └── HighStakesDbContext.cs # EF Core (SQLite)
test-client/index.html         # Static browser client for manual testing
```

Services sit behind interfaces (`ITableManager`, `IWalletService`, `IProvablyFairRngService`) and are wired up with ASP.NET Core dependency injection, so the game loop, hub and HTTP endpoints all share the same logic.

### HTTP endpoints

| Method | Route | Description |
| --- | --- | --- |
| GET | `/health` | Health check |
| POST | `/api/auth/register` | Create an account (starts with a play-money balance) |
| POST | `/api/auth/login` | Validate credentials |
| POST | `/api/wallet/reset` | Reset play-money balance |
| GET | `/api/history/{username}` | Last 20 balance activity entries |
| GET | `/api/tables/{tableId}` | Current table snapshot |

## Running

### Docker (recommended)

```bash
docker network create nginx-net   # only needed once; the compose file expects it
docker compose up -d --build
```

- API + SignalR hub: `http://localhost:8080`
- Test client: `http://localhost:8081`

The SQLite database is stored in the `highstakes-db` volume.

### Locally

```bash
cd src/HighStakes.Api
dotnet run
```

> Note: the database path is set to `/app/data` (the container path), so running outside Docker may need that path changed in `Program.cs`.

## Deployment

Pushing to `main` triggers a GitHub Actions workflow on a **self-hosted runner** on my home Ubuntu server, which pulls the latest code and rebuilds the containers with Docker Compose. The services sit behind an Nginx reverse proxy on a shared Docker network.

## Roadmap

- Let players contribute the client seed (currently fixed per table)
- Token-based authentication for the hub and endpoints
- Additional game types on the same table/round engine

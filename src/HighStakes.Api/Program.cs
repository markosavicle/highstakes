using HighStakes.Api.Hubs;
using HighStakes.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var dataDir = "/app/data";
Directory.CreateDirectory(dataDir);
var dbPath = Path.Combine(dataDir, "highstakes.db");

builder.Services.AddDbContext<HighStakesDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddSignalR();
builder.Services.AddSingleton<IProvablyFairRngService, ProvablyFairRngService>();
builder.Services.AddSingleton<IWalletService, WalletService>();
builder.Services.AddSingleton<ITableManager, TableManager>();
builder.Services.AddHostedService<GameLoopService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Dashboard", policy => policy
        .SetIsOriginAllowed(_ => true)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<HighStakesDbContext>();
    db.Database.EnsureCreated();
}

app.UseCors("Dashboard");

app.MapHub<GameHub>("/hubs/game");

app.MapGet("/health", () => Results.Ok(new { status = "ok", utcNow = DateTime.UtcNow }));

app.MapPost("/api/auth/register", (AuthRequest req, IWalletService wallet) =>
{
    if (wallet.Register(req.Username, req.Password, out var err))
    {
        return Results.Ok(new { success = true, username = req.Username, balance = wallet.GetBalance(req.Username) });
    }
    return Results.BadRequest(new { success = false, message = err });
});

app.MapPost("/api/auth/login", (AuthRequest req, IWalletService wallet) =>
{
    if (wallet.ValidateCredentials(req.Username, req.Password))
        return Results.Ok(new { success = true, username = req.Username, balance = wallet.GetBalance(req.Username) });
    return Results.BadRequest(new { success = false, message = "Invalid username or password." });
});

app.MapPost("/api/profile/update-password", (UpdateProfileRequest req, IWalletService wallet) =>
{
    if (!wallet.ValidateCredentials(req.CurrentUsername, req.CurrentPassword))
        return Results.BadRequest(new { success = false, message = "Current password is incorrect." });

    if (!wallet.ChangePassword(req.CurrentUsername, req.NewPassword, out var err))
        return Results.BadRequest(new { success = false, message = err });

    return Results.Ok(new { success = true, message = "Password updated." });
});

// Play-money only: resets balance back to the starting amount. No real-money path exists.
app.MapPost("/api/wallet/reset", (ResetBalanceRequest req, IWalletService wallet) =>
{
    wallet.ResetBalance(req.Username);
    return Results.Ok(new { success = true, newBalance = wallet.GetBalance(req.Username) });
});

app.MapGet("/api/history/{username}", (string username, HighStakesDbContext db) =>
{
    var lowerUser = username.ToLower();
    var activity = db.RoundActivity
        .Where(t => t.PlayerId.ToLower() == lowerUser)
        .OrderByDescending(t => t.CreatedAtUtc)
        .Take(20)
        .Select(t => new {
            id = t.Id,
            type = t.Type,
            amount = t.Amount,
            balanceAfter = t.BalanceAfter,
            createdAtUtc = t.CreatedAtUtc.ToString("o")
        })
        .ToList();
    return Results.Ok(activity);
});

app.MapGet("/api/tables/{tableId}", (string tableId, ITableManager tables) =>
{
    var table = tables.GetOrCreateTable(tableId);
    return Results.Ok(GameHub.BuildSnapshot(table));
});

app.Run();

public record AuthRequest(string Username, string Password);
public record UpdateProfileRequest(string CurrentUsername, string CurrentPassword, string NewPassword);
public record ResetBalanceRequest(string Username);

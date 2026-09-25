namespace HighStakes.Api.Services;

public interface IWalletService
{
    decimal GetBalance(string playerId);
    bool TryDebit(string playerId, decimal amount, string activityType = "BET");
    void Credit(string playerId, decimal amount, string activityType = "WIN");
    void ResetBalance(string playerId);
    bool ChangePassword(string username, string newPassword, out string? error);
    bool Register(string username, string password, out string? error);
    bool ValidateCredentials(string username, string password);
}

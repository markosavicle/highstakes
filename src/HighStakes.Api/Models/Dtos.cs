namespace HighStakes.Api.Models;

public sealed record TableSnapshotDto(
    string TableId,
    string Phase,
    DateTime PhaseEndsUtc,
    int RoundNumber,
    string CurrentServerSeedHash,
    int PlayerCount,
    IEnumerable<BetDto> ActiveBets,
    RoundResultDto? LastResult);

public sealed record BetDto(string PlayerId, decimal Amount, string Choice);

public sealed record RoundResultDto(
    int RoundNumber,
    int Roll,
    string Winner,
    string ServerSeedHash,
    string RevealedServerSeed,
    string ClientSeed,
    long Nonce,
    IEnumerable<PlayerOutcome> Outcomes);

public sealed record ErrorDto(string Message);

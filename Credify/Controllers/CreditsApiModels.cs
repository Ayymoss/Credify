namespace Credify.Controllers;

/// <summary>One player's credit balance.</summary>
public record ClientBalanceDto(int ClientId, long Balance);

/// <summary>Signed-delta adjustment: negative = deduct, positive = credit.</summary>
public record AdjustCreditsRequest
{
    /// <summary>Signed delta to apply. Must be non-zero.</summary>
    public required long Amount { get; init; }

    /// <summary>Free-text audit reason, recorded in the host log.</summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Allow a deduction to take the balance below zero. Default false: an overdrawing
    /// deduction is rejected with 409 and the balance is left untouched.
    /// </summary>
    public bool AllowOverdraft { get; init; }
}

/// <summary>An active quest definition (the pool players are progressing against today).</summary>
public record QuestDefinitionDto(
    string Id,
    int QuestId,
    string Name,
    string Description,
    int Reward,
    int Target,
    bool IsPermanent,
    bool IsRepeatable);

/// <summary>
/// One player's progress against an active quest. Rewards are granted automatically on
/// completion (there is no separate claim step), so <c>Claimed</c> mirrors <c>Completed</c>.
/// </summary>
public record ClientQuestDto(
    string Id,
    int QuestId,
    int Progress,
    int Target,
    bool Completed,
    bool Claimed);

/// <summary>Richest-players leaderboard entry.</summary>
public record CreditsLeaderboardEntryDto(int Rank, int ClientId, string Name, long Balance);

/// <summary>Quest leaderboard entry: players ranked by currently-completed quest count.</summary>
public record QuestLeaderboardEntryDto(int Rank, int ClientId, string Name, int CompletedQuests);

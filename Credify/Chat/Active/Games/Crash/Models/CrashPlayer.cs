using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Games.Crash.Models;

/// <summary>
/// A player's state within a single Crash round.
/// </summary>
public class CrashPlayer(EFClient client)
{
    public EFClient Client { get; } = client;

    /// <summary>Credits wagered this round (debited when the bet is placed).</summary>
    public long Stake { get; set; }

    /// <summary>True once the player has placed a bet for the current round.</summary>
    public bool HasBet { get; set; }

    /// <summary>The multiplier the player banked at, or null if they haven't cashed out.</summary>
    public double? CashedMultiplier { get; set; }

    public void ResetForNewRound()
    {
        Stake = 0;
        HasBet = false;
        CashedMultiplier = null;
    }
}

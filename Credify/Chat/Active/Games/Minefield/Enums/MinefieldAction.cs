namespace Credify.Chat.Active.Games.Minefield.Enums;

/// <summary>
/// Player actions available during the Digging phase.
/// </summary>
public enum MinefieldAction
{
    /// <summary>Clear the next tile.</summary>
    Dig,

    /// <summary>Collect the current payout and end the session.</summary>
    Cash,

    /// <summary>Re-show the current status line.</summary>
    Status
}

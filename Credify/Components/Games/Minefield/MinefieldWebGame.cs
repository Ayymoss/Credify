using Credify.Configuration;
using Credify.Games.Minefield;

namespace Credify.Components.Games.Minefield;

public enum MinefieldPhase
{
    Configuring,
    Digging,
    Settled
}

public enum TileState
{
    Hidden,
    Gem,
    Mine
}

public enum MinefieldResult
{
    CashedOut,
    Busted,
    Cleared
}

/// <summary>
/// Single-player Minefield (aka "Mines") state machine for the web table. Pure game state — it knows nothing
/// about credits/persistence; the page debits the stake before <see cref="Start"/> and pays
/// <see cref="Payout"/> after the round settles. Multipliers/odds come from <see cref="MinefieldPayoutCalculator"/>
/// — the exact calculator the chat game uses — so the web and in-game economies stay consistent (debit stake,
/// pay stake × multiplier on cash-out, the bank is never touched). Tile placement uses a crypto RNG so it can't
/// be predicted client-side. Reveals are exposed per-tile so the UI can animate each dig.
/// </summary>
public sealed class MinefieldWebGame
{
    private readonly MinefieldConfiguration _config;
    private readonly MinefieldPayoutCalculator _calc;
    private bool[] _mines = [];
    private readonly HashSet<int> _playerDug = [];

    public MinefieldWebGame(MinefieldConfiguration config)
    {
        _config = config;
        _calc = new MinefieldPayoutCalculator(config);
    }

    public int TotalTiles => _config.TotalTiles;

    /// <summary>At least one tile must stay safe, so mines are capped at total - 1.</summary>
    public int MaxMines => Math.Max(1, TotalTiles - 1);

    public MinefieldPhase Phase { get; private set; } = MinefieldPhase.Configuring;
    public TileState[] Tiles { get; private set; } = [];
    public long Stake { get; private set; }
    public int MineCount { get; private set; }
    public int DugCount { get; private set; }
    public MinefieldResult? Result { get; private set; }

    /// <summary>The last tile the player chose — used to highlight the fatal tile on a bust.</summary>
    public int? LastDugIndex { get; private set; }

    public int SafeTotal => TotalTiles - MineCount;
    public int RemainingSafe => SafeTotal - DugCount;

    public double CurrentMultiplier => _calc.CalculateMultiplier(TotalTiles, MineCount, DugCount);
    public double NextMultiplier => _calc.CalculateMultiplier(TotalTiles, MineCount, DugCount + 1);
    public long CurrentPayout => _calc.CalculatePayout(Stake, TotalTiles, MineCount, DugCount);
    public long NextPayout => _calc.CalculatePayout(Stake, TotalTiles, MineCount, DugCount + 1);

    /// <summary>Chance (0..1) the next dig hits a mine — the per-turn risk shown to the player.</summary>
    public double NextTileMineChance => _calc.CalculateNextTileMineChance(TotalTiles, MineCount, DugCount);

    /// <summary>Odds (0..1) the player beat to clear what they have so far. Smaller = more impressive.</summary>
    public double SurvivalProbability => _calc.CalculateSurvivalProbability(TotalTiles, MineCount, DugCount);

    /// <summary>Credits returned to the player on settle (0 on a bust). Stake was already debited on start.</summary>
    public long Payout => Result is MinefieldResult.CashedOut or MinefieldResult.Cleared ? CurrentPayout : 0;

    public long NetResult => Payout - Stake;

    public bool CanCashOut => Phase == MinefieldPhase.Digging && DugCount > 0;

    /// <summary>True if the player personally clicked this tile (vs. it being auto-revealed on settle).</summary>
    public bool WasPlayerDug(int index) => _playerDug.Contains(index);

    public void Start(long stake, int mines)
    {
        MineCount = Math.Clamp(mines, 1, MaxMines);
        Stake = stake;
        DugCount = 0;
        Result = null;
        LastDugIndex = null;
        _playerDug.Clear();
        Tiles = new TileState[TotalTiles];
        _mines = MinefieldField.Build(TotalTiles, MineCount);
        Phase = MinefieldPhase.Digging;
    }

    public void Dig(int index)
    {
        if (Phase != MinefieldPhase.Digging || index < 0 || index >= TotalTiles ||
            Tiles[index] != TileState.Hidden)
        {
            return;
        }

        LastDugIndex = index;
        _playerDug.Add(index);

        if (_mines[index])
        {
            Tiles[index] = TileState.Mine;
            RevealRemaining();
            Result = MinefieldResult.Busted;
            Phase = MinefieldPhase.Settled;
            return;
        }

        Tiles[index] = TileState.Gem;
        DugCount++;

        // cleared every safe tile — auto cash-out at the full multiplier
        if (RemainingSafe == 0)
        {
            RevealRemaining();
            Result = MinefieldResult.Cleared;
            Phase = MinefieldPhase.Settled;
        }
    }

    public void CashOut()
    {
        if (!CanCashOut)
        {
            return;
        }

        RevealRemaining();
        Result = MinefieldResult.CashedOut;
        Phase = MinefieldPhase.Settled;
    }

    public void Reset()
    {
        Phase = MinefieldPhase.Configuring;
        Tiles = [];
        Result = null;
        DugCount = 0;
        Stake = 0;
        LastDugIndex = null;
        _playerDug.Clear();
    }

    // turn over every still-hidden tile when the round ends, so the player sees the mines they
    // avoided (on a cash-out) or the field they were navigating (on a bust)
    private void RevealRemaining()
    {
        for (var i = 0; i < TotalTiles; i++)
        {
            if (Tiles[i] != TileState.Hidden)
            {
                continue;
            }

            Tiles[i] = _mines[i] ? TileState.Mine : TileState.Gem;
        }
    }

}

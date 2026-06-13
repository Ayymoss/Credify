using Credify.Configuration;
using Credify.Games.Crash;

namespace Credify.Components.Games.Crash;

public enum CrashPhase
{
    Idle,
    Flying,
    Settled
}

public enum CrashOutcome
{
    None,
    CashedOut,
    Crashed
}

/// <summary>
/// Single-player Crash round for the web table. Each player flies their own rocket (Crash is per-player, not
/// a shared table). Pure state + timing: the crash point is drawn once on <see cref="Launch"/> and kept
/// secret from the client; the multiplier grows with elapsed real time via the shared <see cref="CrashMath"/>,
/// so the server stays authoritative on when it crashes while the client animates the rising number smoothly.
/// The page debits the stake on launch and pays <see cref="Payout"/> on a cash-out (bank untouched, like the
/// other games).
/// </summary>
public sealed class CrashWebGame(CrashConfiguration config)
{
    private double _crashPoint;
    private DateTimeOffset _startedAt;

    public CrashPhase Phase { get; private set; } = CrashPhase.Idle;
    public CrashOutcome Outcome { get; private set; }
    public long Stake { get; private set; }
    public double CashedMultiplier { get; private set; }

    /// <summary>The crash point — only meaningful (and shown) once the round has settled.</summary>
    public double CrashPoint => _crashPoint;

    public DateTimeOffset StartedAt => _startedAt;
    public double ElapsedSeconds => (DateTimeOffset.UtcNow - _startedAt).TotalSeconds;

    private double CrashAtSeconds => CrashMath.TimeToReach(config, _crashPoint);

    /// <summary>Live multiplier: rises while flying (capped at the crash point), frozen once settled.</summary>
    public double CurrentMultiplier => Phase switch
    {
        CrashPhase.Flying => Math.Min(CrashMath.MultiplierAt(config, ElapsedSeconds), _crashPoint),
        CrashPhase.Settled => Outcome == CrashOutcome.CashedOut ? CashedMultiplier : _crashPoint,
        _ => 1.0
    };

    public long CurrentPayout => (long)(Stake * CurrentMultiplier);

    /// <summary>Credits paid out: stake × cashed multiplier on a cash-out, otherwise 0 (stake already debited).</summary>
    public long Payout => Outcome == CrashOutcome.CashedOut ? (long)(Stake * CashedMultiplier) : 0;
    public long NetResult => Payout - Stake;

    public bool CanCashOut => Phase == CrashPhase.Flying;

    public void Launch(long stake)
    {
        Stake = stake;
        _crashPoint = CrashMath.NextCrashPoint(config);
        _startedAt = DateTimeOffset.UtcNow;
        Phase = CrashPhase.Flying;
        Outcome = CrashOutcome.None;
        CashedMultiplier = 0;
    }

    /// <summary>Called by the page's timer; settles as a crash if the rocket has reached its crash point.</summary>
    public bool PollCrash()
    {
        if (Phase == CrashPhase.Flying && ElapsedSeconds >= CrashAtSeconds)
        {
            Phase = CrashPhase.Settled;
            Outcome = CrashOutcome.Crashed;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Auto cash-out: banks EXACTLY <paramref name="target"/> once the rocket has flown long enough to reach
    /// it — provided the (predetermined) crash point is beyond the target. Because the crash point is known
    /// server-side from launch, this is deterministic: a late poll tick can't turn a winning target into a
    /// bust, and the banked multiplier is the target itself, not whatever the poll happened to sample.
    /// Returns true if it cashed out. A target at/past the crash point returns false and leaves the round to
    /// <see cref="PollCrash"/>.
    /// </summary>
    public bool TryAutoCashOut(double target)
    {
        if (Phase != CrashPhase.Flying || target < 1.01)
        {
            return false;
        }

        if (ElapsedSeconds < CrashMath.TimeToReach(config, target))
        {
            return false; // not there yet
        }

        if (target >= _crashPoint)
        {
            return false; // it crashes first — PollCrash settles this round
        }

        CashedMultiplier = Math.Round(target, 2);
        Phase = CrashPhase.Settled;
        Outcome = CrashOutcome.CashedOut;
        return true;
    }

    /// <summary>Banks the current multiplier — unless the rocket already crashed (then it's a loss).</summary>
    public void CashOut()
    {
        if (Phase != CrashPhase.Flying)
        {
            return;
        }

        if (ElapsedSeconds >= CrashAtSeconds)
        {
            Phase = CrashPhase.Settled;
            Outcome = CrashOutcome.Crashed;
            return;
        }

        CashedMultiplier = Math.Floor(CurrentMultiplier * 100) / 100; // 2dp, never round up past reality
        Phase = CrashPhase.Settled;
        Outcome = CrashOutcome.CashedOut;
    }

    public void Reset()
    {
        Phase = CrashPhase.Idle;
        Outcome = CrashOutcome.None;
        Stake = 0;
        CashedMultiplier = 0;
    }
}

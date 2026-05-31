using System.Collections.Concurrent;
using Credify.Chat.Passive.Quests.Enums;
using Credify.Configuration;
using Credify.Configuration.Translations;
using Credify.Constants;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Feature.Duel;

/// <summary>
/// Player-vs-player kill-race wagers: two players each stake an amount and the first to land
/// KillsToWin kills on the other takes the pot. Challenges must be accepted, expire if
/// ignored, and a duel that runs too long refunds both stakes.
/// </summary>
public class DuelManager(CredifyConfiguration config, PersistenceService persistenceService)
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ConcurrentDictionary<int, PendingDuel> _pending = new();   // key: target clientId
    private readonly ConcurrentDictionary<int, ActiveDuel> _active = new();      // key: each participant clientId

    private DuelTranslations Trans => config.Translations.Duel;

    public async Task ChallengeAsync(EFClient challenger, EFClient target, long amount)
    {
        await _lock.WaitAsync();
        try
        {
            if (amount < config.Duel.MinStake)
            {
                challenger.Tell(config.Translations.Gambling.MinimumAmount);
                return;
            }
            if (config.Duel.MaxStake > 0 && amount > config.Duel.MaxStake)
            {
                challenger.Tell(config.Translations.Gambling.MaximumAmount.FormatExt(config.Duel.MaxStake.ToString("N0")));
                return;
            }
            if (_active.ContainsKey(challenger.ClientId) || _pending.Values.Any(p => p.Challenger.ClientId == challenger.ClientId))
            {
                challenger.Tell(Trans.Busy.FormatExt(challenger.CleanedName));
                return;
            }
            if (_active.ContainsKey(target.ClientId))
            {
                challenger.Tell(Trans.Busy.FormatExt(target.CleanedName));
                return;
            }
            if (_pending.ContainsKey(target.ClientId))
            {
                challenger.Tell(Trans.AlreadyChallenged.FormatExt(target.CleanedName));
                return;
            }
            if (!PersistenceService.AvailableFunds(challenger, amount))
            {
                challenger.Tell(config.Translations.Core.InsufficientCredits);
                return;
            }

            var expiry = new CancellationTokenSource();
            _pending[target.ClientId] = new PendingDuel(challenger, target, amount, expiry);
            SharedLibraryCore.Utilities.ExecuteAfterDelay(config.Duel.ChallengeTimeout, ExpirePendingAsync, expiry.Token);

            target.Tell(Trans.Challenged.FormatExt(challenger.CleanedName, config.Duel.KillsToWin, amount.ToString("N0")));
            challenger.Tell(Trans.ChallengeSent.FormatExt(target.CleanedName, amount.ToString("N0")));

            async Task ExpirePendingAsync(CancellationToken token)
            {
                if (token.IsCancellationRequested) return;
                await _lock.WaitAsync(CancellationToken.None);
                try
                {
                    if (_pending.TryGetValue(target.ClientId, out var p) && p.Challenger.ClientId == challenger.ClientId)
                    {
                        _pending.TryRemove(target.ClientId, out _);
                        challenger.Tell(Trans.Expired);
                    }
                }
                finally
                {
                    if (_lock.CurrentCount is 0) _lock.Release();
                }
            }
        }
        finally
        {
            if (_lock.CurrentCount is 0) _lock.Release();
        }
    }

    public async Task AcceptAsync(EFClient accepter)
    {
        await _lock.WaitAsync();
        try
        {
            if (!_pending.TryGetValue(accepter.ClientId, out var pending))
            {
                accepter.Tell(Trans.NoPending);
                return;
            }

            var challenger = pending.Challenger;
            if (_active.ContainsKey(accepter.ClientId) || _active.ContainsKey(challenger.ClientId))
            {
                accepter.Tell(Trans.Busy.FormatExt(challenger.CleanedName));
                return;
            }
            if (!PersistenceService.AvailableFunds(accepter, pending.Amount) ||
                !PersistenceService.AvailableFunds(challenger, pending.Amount))
            {
                accepter.Tell(config.Translations.Core.InsufficientCredits);
                return;
            }

            pending.Expiry.Cancel();
            _pending.TryRemove(accepter.ClientId, out _);

            await persistenceService.RemoveCreditsAsync(challenger, pending.Amount);
            await persistenceService.RemoveCreditsAsync(accepter, pending.Amount);

            var expiry = new CancellationTokenSource();
            var duel = new ActiveDuel(challenger, accepter, pending.Amount, config.Duel.KillsToWin, expiry);
            _active[challenger.ClientId] = duel;
            _active[accepter.ClientId] = duel;

            SharedLibraryCore.Utilities.ExecuteAfterDelay(config.Duel.MaxDuration, ExpireDuelAsync, expiry.Token);

            var pot = pending.Amount * 2;
            var started = Trans.Started.FormatExt(challenger.CleanedName, accepter.CleanedName,
                config.Duel.KillsToWin, pot.ToString("N0"));
            challenger.Tell(started);
            accepter.Tell(started);

            async Task ExpireDuelAsync(CancellationToken token)
            {
                if (token.IsCancellationRequested) return;
                await _lock.WaitAsync(CancellationToken.None);
                try
                {
                    if (!_active.TryGetValue(challenger.ClientId, out var active) || active != duel) return;
                    _active.TryRemove(challenger.ClientId, out _);
                    _active.TryRemove(accepter.ClientId, out _);
                    await persistenceService.AddCreditsAsync(challenger, duel.Amount);
                    await persistenceService.AddCreditsAsync(accepter, duel.Amount);
                    challenger.Tell(Trans.Expired);
                    accepter.Tell(Trans.Expired);
                }
                finally
                {
                    if (_lock.CurrentCount is 0) _lock.Release();
                }
            }
        }
        finally
        {
            if (_lock.CurrentCount is 0) _lock.Release();
        }
    }

    public async Task HandleKillAsync(EFClient killer, EFClient victim)
    {
        if (killer.ClientId == victim.ClientId) return;
        // Fast path: only proceed if the killer is in a duel against this victim.
        if (!_active.TryGetValue(killer.ClientId, out var duel)) return;
        var opponent = duel.A.ClientId == killer.ClientId ? duel.B : duel.A;
        if (opponent.ClientId != victim.ClientId) return;

        await _lock.WaitAsync();
        try
        {
            if (!_active.TryGetValue(killer.ClientId, out var current) || current != duel) return;

            int killerScore;
            if (killer.ClientId == duel.A.ClientId) killerScore = ++duel.ScoreA;
            else killerScore = ++duel.ScoreB;

            if (killerScore >= duel.KillsToWin)
            {
                await ResolveAsync(duel, killer, opponent);
                return;
            }

            var score = Trans.Score.FormatExt(duel.A.CleanedName, duel.ScoreA, duel.ScoreB, duel.B.CleanedName);
            duel.A.Tell(score);
            duel.B.Tell(score);
        }
        finally
        {
            if (_lock.CurrentCount is 0) _lock.Release();
        }
    }

    /// <summary>Cleans up any duel/challenge the player is part of when they disconnect.</summary>
    public async Task OnDisconnectAsync(EFClient client)
    {
        await _lock.WaitAsync();
        try
        {
            // Drop any pending challenge involving the player (no stakes taken yet).
            if (_pending.TryRemove(client.ClientId, out var incoming)) incoming.Expiry.Cancel();
            foreach (var (key, p) in _pending)
            {
                if (p.Challenger.ClientId != client.ClientId) continue;
                p.Expiry.Cancel();
                _pending.TryRemove(key, out _);
            }

            // Refund an in-progress duel (it wasn't decided by kills).
            if (_active.TryGetValue(client.ClientId, out var duel))
            {
                duel.Expiry.Cancel();
                _active.TryRemove(duel.A.ClientId, out _);
                _active.TryRemove(duel.B.ClientId, out _);
                await persistenceService.AddCreditsAsync(duel.A, duel.Amount);
                await persistenceService.AddCreditsAsync(duel.B, duel.Amount);
                var other = duel.A.ClientId == client.ClientId ? duel.B : duel.A;
                other.Tell(Trans.Expired);
            }
        }
        finally
        {
            if (_lock.CurrentCount is 0) _lock.Release();
        }
    }

    // Caller must hold _lock.
    private async Task ResolveAsync(ActiveDuel duel, EFClient winner, EFClient loser)
    {
        duel.Expiry.Cancel();
        _active.TryRemove(duel.A.ClientId, out _);
        _active.TryRemove(duel.B.ClientId, out _);

        var pot = duel.Amount * 2;
        var fee = (long)(pot * config.Duel.FeePercent / 100.0);
        var payout = pot - fee;

        await persistenceService.AddCreditsAsync(winner, payout);
        ICredifyEventService.RaiseEvent(ObjectiveType.Baller, winner, payout);

        winner.Tell(Trans.Won.FormatExt(payout.ToString("N0")));
        loser.Tell(Trans.Lost.FormatExt(winner.CleanedName));

        if (config.Duel.AnnounceResult)
        {
            winner.CurrentServer.Broadcast(Trans.AnnounceResult.FormatExt(
                PluginConstants.PluginName, winner.CleanedName, loser.CleanedName, pot.ToString("N0")));
        }
    }

    private record PendingDuel(EFClient Challenger, EFClient Target, long Amount, CancellationTokenSource Expiry);

    private sealed class ActiveDuel(EFClient a, EFClient b, long amount, int killsToWin, CancellationTokenSource expiry)
    {
        public EFClient A { get; } = a;
        public EFClient B { get; } = b;
        public long Amount { get; } = amount;
        public int KillsToWin { get; } = killsToWin;
        public CancellationTokenSource Expiry { get; } = expiry;
        public int ScoreA;
        public int ScoreB;
    }
}

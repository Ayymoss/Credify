using Credify.Chat.Active.Core;
using Credify.Chat.Active.Core.Interfaces;
using Credify.Chat.Active.Games.Minefield.Enums;
using Credify.Chat.Active.Games.Minefield.Models;
using Credify.Chat.Active.Games.Minefield.Utilities;
using Credify.Games.Minefield;
using Credify.Chat.Passive.Quests.Enums;
using Credify.Configuration;
using Credify.Configuration.Translations;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Games.Minefield;

/// <summary>
/// Single-player "Minefield" gambling game. Each player runs an isolated session:
/// set a stake, choose a mine count (risk), then clear tiles one at a time. Every
/// safe tile raises the multiplier; cashing out pays stake * multiplier. Hitting a
/// mine loses the whole stake.
///
/// Unlike the shared-round games, Minefield holds no game-wide state machine — all
/// state lives per-player on <see cref="MinefieldPlayer"/>, so many players dig
/// concurrently without interfering.
/// </summary>
public class MinefieldGame(
    PersistenceService persistenceService,
    CredifyConfiguration config,
    GamePlayerCommunication communication,
    MinefieldHandleInput inputHandler,
    MinefieldHandleOutput outputHandler)
    : BaseActiveGame<MinefieldPlayer>(persistenceService, config, communication)
{
    private readonly MinefieldPayoutCalculator _payoutCalculator = new(config.Minefield);
    private readonly IGameInputParser<MinefieldActionResult> _inputHandler = inputHandler;
    private readonly StakeValidator _stakeValidator = new(persistenceService);

    private MinefieldConfiguration Settings => Config.Minefield;
    private MinefieldTranslations Translations => Config.Translations.Minefield;

    #region IActiveGame Implementation

    public override async Task JoinGameAsync(EFClient client)
    {
        var player = Players.GetOrAdd(client, c => new MinefieldPlayer { Client = c });
        player.State = SessionState.AwaitingStake;

        var credits = await PersistenceService.GetClientCreditsAsync(client);
        await outputHandler.TellPlayerAsync(player,
            [Translations.EnterStake.FormatExt(credits.ToString("N0"))], true);
    }

    public override async Task LeaveGameAsync(EFClient client)
    {
        await ExecuteUnderChatLockAsync(async () =>
        {
            if (!Players.TryGetValue(client, out var player)) return;

            // Mid-dig leave/disconnect auto-cashes at the current multiplier (Q4 policy).
            if (player.State == SessionState.Digging)
            {
                await CashOutAsync(player, isAuto: true);
            }
            else
            {
                player.CancelIdleTimer();
                Players.TryRemove(client, out _);
            }
        });
    }

    public override async Task HandleChatAsync(EFClient client, string message)
    {
        if (!Players.TryGetValue(client, out var player)) return;

        await ExecuteUnderChatLockAsync(async () =>
        {
            // Player may have been removed (cash/bust/leave) while we waited for the lock.
            if (!Players.ContainsKey(client)) return;

            switch (player.State)
            {
                case SessionState.AwaitingStake:
                    await HandleStakeInputAsync(player, message);
                    break;
                case SessionState.AwaitingMines:
                    await HandleMinesInputAsync(player, message);
                    break;
                case SessionState.Digging:
                    await HandleDiggingInputAsync(player, message);
                    break;
            }
        });
    }

    #endregion

    #region Setup Q/A

    private async Task HandleStakeInputAsync(MinefieldPlayer player, string message)
    {
        var stakeResult = await _stakeValidator.ValidateStakeAsync(
            message,
            player.Client,
            Config.Translations.Core.InsufficientCredits,
            Translations.InvalidStake,
            Translations.InvalidStake);

        if (!stakeResult.IsValid)
        {
            await outputHandler.TellPlayerAsync(player, [stakeResult.ErrorMessage ?? Translations.InvalidStake]);
            return;
        }

        player.Stake = stakeResult.Result;
        player.State = SessionState.AwaitingMines;
        await outputHandler.TellPlayerAsync(player,
            [Translations.EnterMines.FormatExt(MaxMines.ToString())]);
    }

    private async Task HandleMinesInputAsync(MinefieldPlayer player, string message)
    {
        if (!int.TryParse(message.Trim(), out var mines) || mines < 1 || mines > MaxMines)
        {
            await outputHandler.TellPlayerAsync(player, [Translations.InvalidMines.FormatExt(MaxMines.ToString())]);
            return;
        }

        player.MineCount = mines;
        await ArmFieldAsync(player);
    }

    private async Task ArmFieldAsync(MinefieldPlayer player)
    {
        // Re-validate funds: time may have passed since the stake was entered.
        var credits = await PersistenceService.GetClientCreditsAsync(player.Client);
        if (credits < player.Stake)
        {
            await outputHandler.TellPlayerAsync(player, [Config.Translations.Core.InsufficientCredits]);
            Players.TryRemove(player.Client, out _);
            return;
        }

        player.Field = MinefieldField.Build(Settings.TotalTiles, player.MineCount);
        player.DugCount = 0;
        player.State = SessionState.Digging;

        await PersistenceService.RemoveCreditsAsync(player.Client, player.Stake);

        await outputHandler.TellPlayerAsync(player,
        [
            Translations.Armed.FormatExt(player.MineCount.ToString(), Settings.TotalTiles.ToString(),
                player.Stake.ToString("N0")),
            BuildProgressBar(player),
            Translations.Commands
        ], true);

        StartIdleTimer(player);
    }

    #endregion

    #region Digging

    private async Task HandleDiggingInputAsync(MinefieldPlayer player, string message)
    {
        var parseResult = _inputHandler.Parse(message);
        // Unknown input during a dig session is ignored silently so normal table-talk
        // doesn't get spammed with hints.
        if (!parseResult.IsValid) return;

        switch (parseResult.Result!.Action)
        {
            case MinefieldAction.Dig:
                await DigAsync(player);
                break;
            case MinefieldAction.Cash:
                await CashOutAsync(player, isAuto: false);
                break;
            case MinefieldAction.Status:
                await outputHandler.TellPlayerAsync(player, [BuildStatusLine(player), BuildProgressBar(player), Translations.Commands]);
                break;
        }
    }

    private async Task DigAsync(MinefieldPlayer player)
    {
        player.CancelIdleTimer();

        // The next tile to flip is at index DugCount: all prior tiles were safe, and a
        // mine ends the session, so safe tiles are consumed in order.
        if (player.Field[player.DugCount])
        {
            // Stake was already debited at arm time, so nothing more is taken here.
            // Report the risk of the tile that actually killed them (per-turn mine chance).
            var mineChance = _payoutCalculator.CalculateNextTileMineChance(Settings.TotalTiles, player.MineCount, player.DugCount);
            await outputHandler.TellPlayerAsync(player,
                [Translations.Boom.FormatExt(player.DugCount.ToString(), player.Stake.ToString("N0"),
                    MinefieldPayoutCalculator.FormatProbabilityPct(mineChance))], true);
            ICredifyEventService.RaiseEvent(ObjectiveType.Minefield, player.Client);
            Players.TryRemove(player.Client, out _);
            return;
        }

        player.DugCount++;

        if (player.IsFieldCleared)
        {
            await outputHandler.TellPlayerAsync(player, [Translations.Cleared]);
            await CashOutAsync(player, isAuto: false, isFullClear: true);
            return;
        }

        var multiplier = _payoutCalculator.CalculateMultiplier(Settings.TotalTiles, player.MineCount, player.DugCount);
        var payout = _payoutCalculator.CalculatePayout(player.Stake, Settings.TotalTiles, player.MineCount, player.DugCount);
        await outputHandler.TellPlayerAsync(player,
        [
            Translations.Safe.FormatExt(multiplier.ToString("0.00"), payout.ToString("N0")),
            BuildProgressBar(player),
            Translations.Commands
        ]);

        StartIdleTimer(player);
    }

    /// <summary>
    /// Pays the player their current multiplier and ends the session.
    /// Caller must hold the chat lock.
    /// </summary>
    private async Task CashOutAsync(MinefieldPlayer player, bool isAuto, bool isFullClear = false)
    {
        player.CancelIdleTimer();

        var payout = _payoutCalculator.CalculatePayout(player.Stake, Settings.TotalTiles, player.MineCount, player.DugCount);
        var multiplier = _payoutCalculator.CalculateMultiplier(Settings.TotalTiles, player.MineCount, player.DugCount);
        var probability = _payoutCalculator.CalculateSurvivalProbability(Settings.TotalTiles, player.MineCount, player.DugCount);
        var oddsPct = MinefieldPayoutCalculator.FormatProbabilityPct(probability);
        var profit = payout - player.Stake;

        await PersistenceService.AddCreditsAsync(player.Client, payout);
        ICredifyEventService.RaiseEvent(ObjectiveType.Baller, player.Client, payout);
        ICredifyEventService.RaiseEvent(ObjectiveType.Minefield, player.Client);

        if (isAuto)
        {
            await outputHandler.TellPlayerAsync(player,
                [Translations.AutoCash.FormatExt(multiplier.ToString("0.00"), payout.ToString("N0"), oddsPct)], true);
        }
        else
        {
            await outputHandler.TellPlayerAsync(player,
                [Translations.Cashed.FormatExt(payout.ToString("N0"), profit.ToString("N0"),
                    player.DugCount.ToString(), player.MineCount.ToString(), oddsPct)], true);
        }

        // Announcements only for deliberate cash-outs (not idle/disconnect auto-cashes).
        if (!isAuto)
        {
            var name = player.Client.CleanedName;
            if (isFullClear)
            {
                // Full clear (only mines left) is rare - shout it across every server.
                await outputHandler.BroadcastToAllServersAsync(player,
                    [Translations.BroadcastFullClear.FormatExt(Translations.Title, name,
                        player.MineCount.ToString(), payout.ToString("N0"), oddsPct)]);
            }
            else if (profit > 0 && probability * 100 <= Settings.BroadcastProbabilityThresholdPct)
            {
                await outputHandler.BroadcastToServerAsync(player,
                    [Translations.BroadcastWin.FormatExt(Translations.Title, name, player.DugCount.ToString(),
                        player.MineCount.ToString(), payout.ToString("N0"), oddsPct)]);
            }
        }

        Players.TryRemove(player.Client, out _);
    }

    #endregion

    #region Helpers

    /// <summary>Maximum mines allowed: at least one tile must stay safe.</summary>
    private int MaxMines => Settings.TotalTiles - 1;

    private string BuildStatusLine(MinefieldPlayer player)
    {
        var multiplier = _payoutCalculator.CalculateMultiplier(Settings.TotalTiles, player.MineCount, player.DugCount);
        var payout = _payoutCalculator.CalculatePayout(player.Stake, Settings.TotalTiles, player.MineCount, player.DugCount);
        return Translations.Status.FormatExt(multiplier.ToString("0.00"), payout.ToString("N0"));
    }

    /// <summary>
    /// Builds the visual field bar: '#' per cleared safe tile, '.' per remaining safe
    /// tile (mines are hidden, so only safe tiles are represented).
    /// </summary>
    private string BuildProgressBar(MinefieldPlayer player)
    {
        var cleared = player.DugCount;
        var remaining = player.SafeTotal - cleared;
        var bar = $"(Color::Green){new string('#', cleared)}(Color::White){new string('.', remaining)}";
        return Translations.ProgressBar.FormatExt(bar, cleared.ToString(), player.SafeTotal.ToString());
    }

    private void StartIdleTimer(MinefieldPlayer player)
    {
        player.CancelIdleTimer();
        var cts = new CancellationTokenSource();
        player.IdleToken = cts;

        SharedLibraryCore.Utilities.ExecuteAfterDelay(Settings.TimeoutForPlayerAction, async token =>
        {
            if (token.IsCancellationRequested) return;
            await ExecuteUnderChatLockAsync(async () =>
            {
                if (token.IsCancellationRequested) return;
                if (!Players.TryGetValue(player.Client, out var current) || !ReferenceEquals(current, player)) return;
                if (current.State != SessionState.Digging) return;
                await CashOutAsync(current, isAuto: true);
            });
        }, cts.Token);
    }

    #endregion
}

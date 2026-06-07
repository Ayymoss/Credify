using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.ThreeCardPoker.Enums;
using Credify.Chat.Active.Games.ThreeCardPoker.Models;
using Credify.Chat.Active.Games.ThreeCardPoker.Utilities;
using Credify.Chat.Passive.Quests.Enums;
using Credify.Configuration;
using Credify.Configuration.Translations;
using Credify.Games.Cards;
using Credify.Games.ThreeCardPoker;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Games.ThreeCardPoker;

/// <summary>
/// Single-player Three-Card Poker vs the house. Each player runs an isolated session (ante → see your three
/// cards → play or fold → settle → next hand), following the same active-game framework as Minefield. The
/// rules/payouts come from the shared <see cref="ThreeCardRules"/> core that the webfront uses too.
/// </summary>
public class ThreeCardPokerGame(
    PersistenceService persistenceService,
    CredifyConfiguration config,
    GamePlayerCommunication communication,
    ThreeCardHandleOutput output)
    : BaseActiveGame<ThreeCardChatPlayer>(persistenceService, config, communication)
{
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(45);
    private readonly CardDeck _deck = new();
    private readonly StakeValidator _stakeValidator = new(persistenceService);

    private ThreeCardTranslations Translations => Config.Translations.ThreeCard;

    public override async Task JoinGameAsync(EFClient client)
    {
        var player = Players.GetOrAdd(client, c => new ThreeCardChatPlayer { Client = c });
        player.State = ThreeCardSessionState.AwaitingAnte;

        var credits = await PersistenceService.GetClientCreditsAsync(client);
        await output.TellPlayerAsync(player, [Translations.EnterAnte.FormatExt(credits.ToString("N0"))], true);
        StartIdleTimer(player);
    }

    public override async Task LeaveGameAsync(EFClient client)
    {
        await ExecuteUnderChatLockAsync(async () =>
        {
            if (!Players.TryGetValue(client, out var player)) return;

            // leaving mid-decision forfeits the ante (auto-fold), settling the Pair Plus if any
            if (player.State == ThreeCardSessionState.AwaitingDecision)
            {
                await SettleAsync(player, played: false, loop: false);
            }

            player.CancelIdleTimer();
            Players.TryRemove(client, out _);
            // the leave message is sent by the join command's handleLeaveSuccessAsync
        });
    }

    public override async Task HandleChatAsync(EFClient client, string message)
    {
        if (!Players.TryGetValue(client, out var player)) return;

        await ExecuteUnderChatLockAsync(async () =>
        {
            if (!Players.ContainsKey(client)) return;

            switch (player.State)
            {
                case ThreeCardSessionState.AwaitingAnte:
                    await HandleAnteAsync(player, message);
                    break;
                case ThreeCardSessionState.AwaitingDecision:
                    await HandleDecisionAsync(player, message);
                    break;
            }
        });
    }

    private async Task HandleAnteAsync(ThreeCardChatPlayer player, string message)
    {
        var tokens = message.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return;
        }

        var stakeResult = await _stakeValidator.ValidateStakeAsync(tokens[0], player.Client,
            Config.Translations.Core.InsufficientCredits, Translations.InvalidAnte, Translations.InvalidAnte);
        if (!stakeResult.IsValid)
        {
            await output.TellPlayerAsync(player, [stakeResult.ErrorMessage ?? Translations.InvalidAnte]);
            return;
        }

        var ante = stakeResult.Result;
        var pairPlus = tokens.Skip(1).Any(t => t.Equals("pp", StringComparison.OrdinalIgnoreCase)) ? ante : 0;

        var credits = await PersistenceService.GetClientCreditsAsync(player.Client);
        if (ante + pairPlus > credits)
        {
            await output.TellPlayerAsync(player, [Translations.NotEnoughForPairPlus]);
            return;
        }

        player.CancelIdleTimer();
        await PersistenceService.RemoveCreditsAsync(player.Client, ante + pairPlus);

        player.Ante = ante;
        player.PairPlus = pairPlus;
        player.PlayerCards.Clear();
        player.DealerCards.Clear();
        for (var i = 0; i < 3; i++)
        {
            player.PlayerCards.Add(_deck.Draw());
            player.DealerCards.Add(_deck.Draw());
        }

        player.State = ThreeCardSessionState.AwaitingDecision;
        var hand = ThreeCardHand.Evaluate(player.PlayerCards);
        await output.TellPlayerAsync(player,
        [
            Translations.YourHand.FormatExt(FormatCards(player.PlayerCards), hand.Name),
            Translations.Decision.FormatExt(ante.ToString("N0"))
        ], true);
        StartIdleTimer(player);
    }

    private async Task HandleDecisionAsync(ThreeCardChatPlayer player, string message)
    {
        var input = message.Trim().ToLowerInvariant();
        switch (input)
        {
            case "play" or "p":
                var credits = await PersistenceService.GetClientCreditsAsync(player.Client);
                if (credits < player.Ante)
                {
                    await output.TellPlayerAsync(player, [Translations.NotEnoughToPlay]);
                    return;
                }

                player.CancelIdleTimer();
                await PersistenceService.RemoveCreditsAsync(player.Client, player.Ante); // the Play bet
                await SettleAsync(player, played: true, loop: true);
                break;

            case "fold" or "f":
                player.CancelIdleTimer();
                await SettleAsync(player, played: false, loop: true);
                break;

            // unknown input ignored so table-talk doesn't get spammed with hints
        }
    }

    /// <summary>Settle the current hand. Caller holds the chat lock and has already taken the Play bet if played.</summary>
    private async Task SettleAsync(ThreeCardChatPlayer player, bool played, bool loop)
    {
        var playerHand = ThreeCardHand.Evaluate(player.PlayerCards);
        var dealerHand = ThreeCardHand.Evaluate(player.DealerCards);
        var outcome = ThreeCardRules.Settle(playerHand, dealerHand, player.Ante, player.PairPlus, played);

        if (outcome.TotalReturn > 0)
        {
            await PersistenceService.AddCreditsAsync(player.Client, outcome.TotalReturn);
        }
        if (outcome.Net > 0)
        {
            ICredifyEventService.RaiseEvent(ObjectiveType.Baller, player.Client, outcome.TotalReturn);
        }

        var balance = await PersistenceService.GetClientCreditsAsync(player.Client);
        var lines = new List<string>();

        // reveal the dealer hand (only meaningful when the player actually played)
        if (played)
        {
            var qualify = outcome.DealerQualified ? "" : Translations.DidNotQualify;
            lines.Add(Translations.DealerHand.FormatExt(FormatCards(player.DealerCards), dealerHand.Name, qualify));
        }

        if (player.PairPlus > 0 && outcome.PairPlusReturn > 0)
        {
            lines.Add(Translations.PairPlusHit.FormatExt((outcome.PairPlusReturn - player.PairPlus).ToString("N0")));
        }

        lines.Add(outcome.Result switch
        {
            "Win" => Translations.Win.FormatExt(outcome.Net.ToString("N0"), balance.ToString("N0")),
            "Push" => Translations.Push.FormatExt(outcome.Net.ToString("N0"), balance.ToString("N0")),
            "Fold" => Translations.Fold.FormatExt(outcome.Net.ToString("N0"), balance.ToString("N0")),
            _ => Translations.Lose.FormatExt(outcome.Net.ToString("N0"), balance.ToString("N0"))
        });

        if (loop)
        {
            lines.Add(Translations.NextHand);
        }

        await output.TellPlayerAsync(player, lines, true);

        if (loop)
        {
            player.Ante = 0;
            player.PairPlus = 0;
            player.PlayerCards.Clear();
            player.DealerCards.Clear();
            player.State = ThreeCardSessionState.AwaitingAnte;
            StartIdleTimer(player);
        }
    }

    private static string FormatCards(IEnumerable<Card> cards) =>
        string.Join(" ", cards.Select(card => $"(Color::{(card.IsRed ? "Red" : "White")}){card.RankLabel}{card.SuitLetter}(Color::White)"));

    private void StartIdleTimer(ThreeCardChatPlayer player)
    {
        player.CancelIdleTimer();
        var cts = new CancellationTokenSource();
        player.IdleToken = cts;

        SharedLibraryCore.Utilities.ExecuteAfterDelay(IdleTimeout, async token =>
        {
            if (token.IsCancellationRequested) return;
            await ExecuteUnderChatLockAsync(async () =>
            {
                if (token.IsCancellationRequested) return;
                if (!Players.TryGetValue(player.Client, out var current) || !ReferenceEquals(current, player)) return;

                // idle mid-decision auto-folds; idle while waiting for an ante just leaves the table
                if (current.State == ThreeCardSessionState.AwaitingDecision)
                {
                    await SettleAsync(current, played: false, loop: false);
                }

                current.CancelIdleTimer();
                Players.TryRemove(current.Client, out _);
                output.Tell(current, Translations.Leave);
            });
        }, cts.Token);
    }
}

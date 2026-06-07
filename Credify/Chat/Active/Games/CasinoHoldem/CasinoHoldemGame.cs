using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.CasinoHoldem.Enums;
using Credify.Chat.Active.Games.CasinoHoldem.Models;
using Credify.Chat.Active.Games.CasinoHoldem.Utilities;
using Credify.Chat.Active.Games.Poker.Enums;
using Credify.Chat.Active.Games.Poker.Models;
using Credify.Chat.Active.Games.Poker.Services;
using Credify.Chat.Passive.Quests.Enums;
using Credify.Configuration;
using Credify.Configuration.Translations;
using Credify.Games.CasinoHoldem;
using Credify.Games.Cards;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Games.CasinoHoldem;

/// <summary>
/// Single-player Casino Hold'em vs the house, on the active-game framework (ante → see your hole cards + the
/// flop → call or fold → reveal turn/river + dealer → settle → next hand). Reuses the existing poker hand
/// evaluator and the shared <see cref="CasinoHoldemRules"/> core.
/// </summary>
public class CasinoHoldemGame(
    PersistenceService persistenceService,
    CredifyConfiguration config,
    GamePlayerCommunication communication,
    CasinoHoldemHandleOutput output)
    : BaseActiveGame<CasinoHoldemChatPlayer>(persistenceService, config, communication)
{
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(45);
    private readonly CardDeck _deck = new();
    private readonly PokerHandEvaluator _evaluator = new();
    private readonly StakeValidator _stakeValidator = new(persistenceService);

    private CasinoHoldemTranslations Translations => Config.Translations.CasinoHoldem;

    public override async Task JoinGameAsync(EFClient client)
    {
        var player = Players.GetOrAdd(client, c => new CasinoHoldemChatPlayer { Client = c });
        player.State = CasinoHoldemSessionState.AwaitingAnte;

        var credits = await PersistenceService.GetClientCreditsAsync(client);
        await output.TellPlayerAsync(player, [Translations.EnterAnte.FormatExt(credits.ToString("N0"))], true);
        StartIdleTimer(player);
    }

    public override async Task LeaveGameAsync(EFClient client)
    {
        await ExecuteUnderChatLockAsync(async () =>
        {
            if (!Players.TryGetValue(client, out var player)) return;

            if (player.State == CasinoHoldemSessionState.AwaitingDecision)
            {
                await SettleAsync(player, called: false, loop: false);
            }

            player.CancelIdleTimer();
            Players.TryRemove(client, out _);
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
                case CasinoHoldemSessionState.AwaitingAnte:
                    await HandleAnteAsync(player, message);
                    break;
                case CasinoHoldemSessionState.AwaitingDecision:
                    await HandleDecisionAsync(player, message);
                    break;
            }
        });
    }

    private async Task HandleAnteAsync(CasinoHoldemChatPlayer player, string message)
    {
        var stakeResult = await _stakeValidator.ValidateStakeAsync(message.Trim(), player.Client,
            Config.Translations.Core.InsufficientCredits, Translations.InvalidAnte, Translations.InvalidAnte);
        if (!stakeResult.IsValid)
        {
            await output.TellPlayerAsync(player, [stakeResult.ErrorMessage ?? Translations.InvalidAnte]);
            return;
        }

        var ante = stakeResult.Result;
        player.CancelIdleTimer();
        await PersistenceService.RemoveCreditsAsync(player.Client, ante);

        player.Ante = ante;
        player.PlayerHole.Clear();
        player.DealerHole.Clear();
        player.Community.Clear();
        for (var i = 0; i < 2; i++)
        {
            player.PlayerHole.Add(_deck.Draw());
            player.DealerHole.Add(_deck.Draw());
        }
        for (var i = 0; i < 5; i++)
        {
            player.Community.Add(_deck.Draw());
        }

        player.State = CasinoHoldemSessionState.AwaitingDecision;
        var hint = _evaluator.EvaluateBestAvailable(player.PlayerHole.Select(ToPoker).ToList(),
            player.Community.Take(3).Select(ToPoker).ToList());
        await output.TellPlayerAsync(player,
        [
            Translations.Deal.FormatExt(FormatCards(player.PlayerHole), FormatCards(player.Community.Take(3)),
                hint is not null ? RankName(hint.Rank) : "—"),
            Translations.Decision.FormatExt((ante * 2).ToString("N0"))
        ], true);
        StartIdleTimer(player);
    }

    private async Task HandleDecisionAsync(CasinoHoldemChatPlayer player, string message)
    {
        var input = message.Trim().ToLowerInvariant();
        switch (input)
        {
            case "call" or "c":
                var callCost = player.Ante * 2;
                var credits = await PersistenceService.GetClientCreditsAsync(player.Client);
                if (credits < callCost)
                {
                    await output.TellPlayerAsync(player, [Translations.NotEnoughToCall]);
                    return;
                }

                player.CancelIdleTimer();
                await PersistenceService.RemoveCreditsAsync(player.Client, callCost);
                await SettleAsync(player, called: true, loop: true);
                break;

            case "fold" or "f":
                player.CancelIdleTimer();
                await SettleAsync(player, called: false, loop: true);
                break;
        }
    }

    private async Task SettleAsync(CasinoHoldemChatPlayer player, bool called, bool loop)
    {
        var playerHand = _evaluator.EvaluateBestHand(player.PlayerHole.Select(ToPoker).ToList(),
            player.Community.Select(ToPoker).ToList());
        var dealerHand = _evaluator.EvaluateBestHand(player.DealerHole.Select(ToPoker).ToList(),
            player.Community.Select(ToPoker).ToList());
        var outcome = CasinoHoldemRules.Settle(playerHand, dealerHand, player.Ante, called);

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

        if (called)
        {
            var qualify = outcome.DealerQualified ? "" : Translations.DidNotQualify;
            lines.Add(Translations.Reveal.FormatExt(FormatCards(player.DealerHole), FormatCards(player.Community),
                RankName(dealerHand.Rank), qualify));
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
            player.PlayerHole.Clear();
            player.DealerHole.Clear();
            player.Community.Clear();
            player.State = CasinoHoldemSessionState.AwaitingAnte;
            StartIdleTimer(player);
        }
    }

    private static PokerCard ToPoker(Card card) => new((PokerCard.Suit)(int)card.Suit, (PokerCard.Rank)(int)card.Rank);

    private static string FormatCards(IEnumerable<Card> cards) =>
        string.Join(" ", cards.Select(card => $"(Color::{(card.IsRed ? "Red" : "White")}){card.RankLabel}{card.SuitLetter}(Color::White)"));

    private static string RankName(HandRank rank) => rank switch
    {
        HandRank.RoyalFlush => "Royal flush",
        HandRank.StraightFlush => "Straight flush",
        HandRank.FourOfAKind => "Four of a kind",
        HandRank.FullHouse => "Full house",
        HandRank.Flush => "Flush",
        HandRank.Straight => "Straight",
        HandRank.ThreeOfAKind => "Three of a kind",
        HandRank.TwoPair => "Two pair",
        HandRank.Pair => "Pair",
        _ => "High card"
    };

    private void StartIdleTimer(CasinoHoldemChatPlayer player)
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

                if (current.State == CasinoHoldemSessionState.AwaitingDecision)
                {
                    await SettleAsync(current, called: false, loop: false);
                }

                current.CancelIdleTimer();
                Players.TryRemove(current.Client, out _);
                output.Tell(current, Translations.Leave);
            });
        }, cts.Token);
    }
}

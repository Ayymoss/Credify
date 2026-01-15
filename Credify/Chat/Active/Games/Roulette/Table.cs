using System.Collections.Concurrent;
using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.Roulette.Enums;
using Credify.Chat.Active.Games.Roulette.Models;
using Credify.Chat.Active.Games.Roulette.Models.BetTypes.Inside;
using Credify.Chat.Active.Games.Roulette.Utilities;
using Credify.Chat.Passive.Quests.Enums;
using Credify.Configuration;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Games.Roulette;

public class Table(
    CredifyConfiguration config,
    TranslationsRoot translations,
    PersistenceService persistenceService,
    GamePlayerCommunication communication,
    RouletteHandleInput input,
    IGameOutputHandler<Player> output)
    : BaseContinuousGame<Player>(persistenceService, config, communication)
{
    private List<Player> _players = [];

    protected override int GetMinimumPlayers() => 1; // Roulette can run with any number of players

    protected override TimeSpan GetDelayBetweenRounds() => TimeSpan.Zero; // No delay for roulette

    protected override async Task ExecuteGameRoundAsync(CancellationToken token)
    {
        _players = Players.Values.ToList();

        var bets = await input.GetPlayerInputsAsync(_players, token);
        await PopulateBets(bets);
        RemoveEmptyBets();

        await SpinWheelMessage(token);
        await HandleResult(SpinWheel());

        foreach (var player in _players)
        {
            player.ClearBet();
            ICredifyEventService.RaiseEvent(ObjectiveType.Roulette, player.Client);
        }

        await RemoveBrokePlayers();
    }


    private async Task RemoveBrokePlayers()
    {
        List<Player> playersToRemove = [];
        foreach (var player in _players)
        {
            var credits = await PersistenceService.GetClientCreditsAsync(player.Client);
            if (credits >= 10) continue;
            output.Tell(player, translations.Roulette.Broke);
            playersToRemove.Add(player);
        }

        foreach (var player in playersToRemove)
        {
            PlayerLeave(player.Client);
        }
    }

    private async Task SpinWheelMessage(CancellationToken token)
    {
        var message = translations.Roulette.Prefix(translations.Roulette.SpinningWheel);
        await output.TellPlayersAsync(_players, [$"{message}..."]);
        await Task.Delay(1_000, token);
        await output.TellPlayersAsync(_players, [$"{message}.."]);
        await Task.Delay(1_500, token);
        await output.TellPlayersAsync(_players, [$"{message}."]);
        await Task.Delay(2_000, token);
    }

    private async Task HandleResult(SpinResult spinResult)
    {
        foreach (var player in _players)
        {
            if (player.Bet is null) continue;

            if (!player.Bet.HasWon(spinResult))
            {
                output.Tell(player, translations.Roulette.Lost.FormatExt(player.Bet.Stake.ToString("N0")));
                continue;
            }

            ICredifyEventService.RaiseEvent(ObjectiveType.Baller, player.Client, player.Bet.Payout);
            output.Tell(player, translations.Roulette.Won.FormatExt((player.Bet.Payout - player.Bet.Stake).ToString("N0")));
            await PersistenceService.AddCreditsAsync(player.Client, player.Bet.Payout);

            if (!config.Roulette.AnnounceMaxPayoutWinners) continue;
            if (player.Bet is not StraightUpBet straightUpBet) continue;

            await output.BroadcastToAllServersAsync(player,
            [
                translations.Roulette.LongPrefix(translations.Roulette.HouseWin.FormatExt(player.Client.CleanedName,
                    (player.Bet.Payout - player.Bet.Stake).ToString("N0"), straightUpBet.Number))
            ]);
        }

        var colourString = spinResult.Colour switch
        {
            Colour.Black => "(Color::White)",
            Colour.Red => "(Color::Red)",
            Colour.Green => "(Color::Green)",
            _ => throw new ArgumentOutOfRangeException(nameof(spinResult))
        };

        var ballColour = $"{colourString}{spinResult.Colour}(Color::White)";

        await output.TellPlayersAsync(_players, [
            translations.Roulette.Prefix(translations.Roulette.BallStopped.FormatExt(spinResult.Number, ballColour))
        ]);
    }

    private void RemoveEmptyBets()
    {
        var playersWithEmptyBets = _players.Where(player => player.Bet is null).ToList();

        foreach (var player in playersWithEmptyBets)
        {
            output.Tell(player, translations.Roulette.BetTimeout);
            PlayerLeave(player.Client);
        }
    }

    private async Task PopulateBets(List<RouletteHandleInput.PlayerBetType> bets)
    {
        foreach (var bet in bets)
        {
            if (bet.BaseBet is null) continue;
            bet.Player.CreateBet(bet.BaseBet);
            await PersistenceService.RemoveCreditsAsync(bet.Player.Client, bet.BaseBet.Stake);
        }
    }

    private static SpinResult SpinWheel()
    {
        var number = Random.Shared.Next(0, 37);
        var isEven = number % 2 is 0;
        var color = number is 0
            ? Colour.Green
            : isEven
                ? Colour.Black
                : Colour.Red;

        return new SpinResult(number, color, isEven);
    }

    public async Task<bool> PlayerJoinAsync(Player player)
    {
        var added = Players.TryAdd(player.Client, player);
        if (!HasPlayers.IsSet)
        {
            await output.BroadcastToServerAsync(player,
                [translations.Roulette.LongPrefix(translations.Roulette.PlayerStartedRoulette.FormatExt(player.Client.CleanedName))]);
            SignalPlayersAvailable();
            return added;
        }

        output.Tell(player, translations.Roulette.JoinDuringActiveMessage, true);
        return added;
    }

    /// <summary>
    /// IActiveGame implementation - allows a player to join the game.
    /// </summary>
    public override async Task JoinGameAsync(EFClient player)
    {
        await PlayerJoinAsync(new Player(player));
        OnPlayerJoined();
    }

    public void PlayerLeave(EFClient client)
    {
        if (Players.TryGetValue(client, out var player))
        {
            output.Tell(player, translations.Roulette.LeaveMessage, true);
        }
        RemovePlayer(client);

        lock (_players) _players.RemoveAll(p => Equals(p.Client, client));

        ResetPlayersSignal();
    }

    public bool IsPlayerInGame(EFClient client) => IsPlayerPlaying(client);

    /// <summary>
    /// IActiveGame implementation - removes a player from the game.
    /// </summary>
    public override Task LeaveGameAsync(EFClient player)
    {
        PlayerLeave(player);
        OnPlayerLeft();
        return Task.CompletedTask;
    }

    /// <summary>
    /// IActiveGame implementation - handles chat messages (roulette doesn't use chat input during gameplay).
    /// </summary>
    public override Task HandleChatAsync(EFClient player, string message) => Task.CompletedTask;
}

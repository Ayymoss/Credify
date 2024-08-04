using System.Collections.Concurrent;
using Credify.Chat.Active.Roulette.Enums;
using Credify.Chat.Active.Roulette.Models;
using Credify.Chat.Active.Roulette.Models.BetTypes.Inside;
using Credify.Chat.Active.Roulette.Utilities;
using Credify.Configuration;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Roulette;

public class Table(TranslationsRoot translations, PersistenceManager persistenceManager, HandleInput input, HandleOutput output)
{
    private readonly ConcurrentDictionary<EFClient, Player> _waitingPlayers = [];
    private List<Player> _players = [];
    private readonly ManualResetEventSlim _hasPlayers = new(false);

    public async Task GameLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            _hasPlayers.Wait(token);
            _players = _waitingPlayers.Values.ToList();

            await PopulateBets(await input.GetPlayerInputsAsync(_players, token));
            RemoveEmptyBets();

            await SpinWheelMessage(token);
            await HandleResult(SpinWheel());

            foreach (var player in _players)
            {
                player.ClearBet();
                await RemoveBrokePlayers(player);
            }
        }
    }

    private async Task RemoveBrokePlayers(Player player)
    {
        var credits = await persistenceManager.GetClientCreditsAsync(player.Client);
        if (credits < 10)
        {
            output.Tell(player, translations.Roulette.Broke);
            PlayerLeave(player.Client);
        }
    }

    private async Task SpinWheelMessage(CancellationToken token)
    {
        var message = translations.Roulette.Prefix(translations.Roulette.SpinningWheel);
        await HandleOutput.TellAsync(_players, [$"{message}..."]);
        await Task.Delay(1_250, token);
        await HandleOutput.TellAsync(_players, [$"{message}.."]);
        await Task.Delay(1_500, token);
        await HandleOutput.TellAsync(_players, [$"{message}."]);
        await Task.Delay(1_750, token);
    }

    private async Task HandleResult(SpinResult spinResult)
    {
        foreach (var player in _players)
        {
            if (player.Bet is null) continue;

            if (!player.Bet.HasWon(spinResult))
            {
                output.Tell(player, translations.Roulette.Lost.FormatExt(player.Bet.Stake));
                continue;
            }

            output.Tell(player, translations.Roulette.Won.FormatExt(player.Bet.Payout - player.Bet.Stake));
            await persistenceManager.AddCreditsAsync(player.Client, player.Bet.Payout);

            if (player.Bet is not StraightUpBet straightUpBet) continue;

            await HandleOutput.TellAllServersAsync(player,
            [
                translations.Roulette.LongPrefix(translations.Roulette.HouseWin.FormatExt(player.Client.CleanedName,
                    straightUpBet.Number, player.Bet.Payout - player.Bet.Stake))
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

        await HandleOutput.TellAsync(_players, [
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

    private async Task PopulateBets(List<HandleInput.PlayerBetType> bets)
    {
        foreach (var bet in bets)
        {
            if (bet.BaseBet is null) continue;
            bet.Player.CreateBet(bet.BaseBet);
            await persistenceManager.RemoveCreditsAsync(bet.Player.Client, bet.BaseBet.Stake);
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
        var added = _waitingPlayers.TryAdd(player.Client, player);
        if (!_hasPlayers.IsSet)
        {
            await HandleOutput.TellAllServerAsync(player,
                [translations.Roulette.LongPrefix(translations.Roulette.PlayerStartedRoulette.FormatExt(player.Client.CleanedName))]);
            _hasPlayers.Set();
            return added;
        }

        output.Tell(player, translations.Roulette.JoinDuringActiveMessage, true);
        return added;
    }

    public void PlayerLeave(EFClient client)
    {
        output.Tell(_waitingPlayers[client], translations.Roulette.LeaveMessage, true);
        _waitingPlayers.TryRemove(client, out _);
        _players.RemoveAll(player => Equals(player.Client, client));

        if (_waitingPlayers.IsEmpty)
        {
            _hasPlayers.Reset();
        }
    }

    public bool IsPlayerInGame(EFClient client)
    {
        return _waitingPlayers.ContainsKey(client);
    }
}

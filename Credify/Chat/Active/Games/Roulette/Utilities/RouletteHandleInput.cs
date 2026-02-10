using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.Roulette.Enums;
using Credify.Chat.Active.Games.Roulette.Models;
using Credify.Chat.Active.Games.Roulette.Models.BetTypes;
using Credify.Chat.Active.Games.Roulette.Models.BetTypes.Inside;
using Credify.Chat.Active.Games.Roulette.Services;
using Credify.Configuration;
using Credify.Configuration.Translations;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Helpers;

// ReSharper disable AccessToDisposedClosure

namespace Credify.Chat.Active.Games.Roulette.Utilities;

public class RouletteHandleInput(PersistenceService persistenceService, CredifyConfiguration config)
{
    private readonly RouletteTranslations _rouletteTrans = config.Translations.Roulette;
    private readonly StakeValidator _stakeValidator = new(persistenceService, 10);

    private record PlayerBet(Player Player, int? Stake);

    private record PlayerBetCategory(Player Player, BetCategory? BetCategory);

    public record PlayerBetType(Player Player, BaseBet? BaseBet);

    public async Task<List<PlayerBetType>> GetPlayerInputsAsync(List<Models.Player> players, CancellationToken token)
    {
        var playerBets = await GetBetsAsync(players, token);
        var playerBetCategories = await GetBetCategoriesAsync(playerBets.Where(x => x.Stake.HasValue)
            .Select(x => x.Player)
            .ToList(), token);

        var insidePlayers = playerBetCategories
            .Where(x => x.BetCategory is BetCategory.Inside)
            .Select(x => x.Player)
            .ToList();

        var outsidePlayers = playerBetCategories
            .Where(x => x.BetCategory is BetCategory.Outside)
            .Select(x => x.Player)
            .ToList();

        var insideBetTasks = GetInsideBetNumbersAsync(playerBets.Where(x => insidePlayers.Contains(x.Player)).ToList(), token);
        var outsideBetTasks = GetOutsideBetNumbersAsync(playerBets.Where(x => outsidePlayers.Contains(x.Player)).ToList(), token);

        var tasks = await Task.WhenAll(insideBetTasks, outsideBetTasks);
        return tasks.SelectMany(x => x).ToList();
    }

    #region Input

    private async Task<List<PlayerBet>> GetBetsAsync(List<Player> players, CancellationToken token)
    {
        using var tokenSource = new CancellationTokenSource(config.Roulette.TimeoutForPlayerAction);
        using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(tokenSource.Token, token);

        var tasks = players.Select(async player =>
        {
            var playerCredits = await persistenceService.GetClientCreditsAsync(player.Client);
            var stakeParser = new RouletteStakeParser(_stakeValidator, playerCredits, _rouletteTrans, config);
            var result = await player.Client.PromptClientInput(
                [_rouletteTrans.Prefix(_rouletteTrans.HowMuchToBet.FormatExt(playerCredits.ToString("N0")))], input =>
                {
                    var parseResult = stakeParser.Parse(input);
                    var innerResult = new ParsedInputResult<int?>();
                    if (!parseResult.IsValid)
                    {
                        return Task.FromResult(innerResult.WithError(parseResult.ErrorMessage));
                    }

                    innerResult.Result = (int)parseResult.Result;
                    return Task.FromResult(innerResult);
                }, _rouletteTrans.Prefix(_rouletteTrans.InputTimeout), linkedTokenSource.Token);

            if (players.Count is not 1 && result.Result.HasValue)
            {
                player.Client.Tell(_rouletteTrans.Prefix(_rouletteTrans.BetAccepted));
            }

            return new PlayerBet(player, result.Result);
        });

        var completedTasks = await Task.WhenAll(tasks);
        return completedTasks.Where(x => x.Stake.HasValue).ToList();
    }

    private async Task<List<PlayerBetCategory>> GetBetCategoriesAsync(List<Player> players, CancellationToken token)
    {
        using var tokenSource = new CancellationTokenSource(config.Roulette.TimeoutForPlayerAction);
        using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(tokenSource.Token, token);

        var tasks = players.Select(async player =>
        {
            var categoryParser = new RouletteBetCategoryParser(_rouletteTrans);
            var result = await player.Client.PromptClientInput(
            [
                _rouletteTrans.Prefix(_rouletteTrans.InnerOrOutsideBet),
                _rouletteTrans.Prefix(_rouletteTrans.InnerOrOutsideBetAcceptableInputs)
            ], input =>
            {
                var parseResult = categoryParser.Parse(input);
                var innerResult = new ParsedInputResult<BetCategory?>();
                if (!parseResult.IsValid)
                {
                    return Task.FromResult(innerResult.WithError(parseResult.ErrorMessage));
                }

                innerResult.Result = parseResult.Result;
                return Task.FromResult(innerResult);
            }, _rouletteTrans.Prefix(_rouletteTrans.InputTimeout), linkedTokenSource.Token);

            if (players.Count is not 1 && result.Result.HasValue)
            {
                player.Client.Tell(_rouletteTrans.Prefix(_rouletteTrans.BetAccepted));
            }

            return new PlayerBetCategory(player, result.Result);
        });

        var completedTasks = await Task.WhenAll(tasks);
        return completedTasks.Where(x => x.BetCategory is not null).ToList();
    }

    private async Task<IEnumerable<PlayerBetType>> GetInsideBetNumbersAsync(List<PlayerBet> players, CancellationToken token)
    {
        using var tokenSource = new CancellationTokenSource(config.Roulette.TimeoutForPlayerAction);
        using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(tokenSource.Token, token);

        var tasks = players.Select(async player =>
        {
            var result = await player.Player.Client.PromptClientInput(
                [
                    _rouletteTrans.Prefix(_rouletteTrans.InsideBetSelected),
                    _rouletteTrans.Prefix(_rouletteTrans.InsidePickNumbers),
                    _rouletteTrans.Prefix(_rouletteTrans.InsideBetOptions)
                ],
                input =>
                {
                    var parser = new RouletteInsideBetParser(player.Stake!.Value, _rouletteTrans);
                    var parseResult = parser.Parse(input);
                    var innerResult = new ParsedInputResult<InsideBaseBet?>();
                    if (!parseResult.IsValid)
                    {
                        return Task.FromResult(innerResult.WithError(parseResult.ErrorMessage));
                    }

                    innerResult.Result = parseResult.Result;
                    return Task.FromResult(innerResult);
                }, _rouletteTrans.Prefix(_rouletteTrans.InputTimeout),
                linkedTokenSource.Token);

            if (players.Count is not 1 && result.Result is not null)
            {
                player.Player.Client.Tell(_rouletteTrans.Prefix(_rouletteTrans.BetAccepted));
            }

            return new PlayerBetType(player.Player, result.Result);
        });

        var completedTasks = await Task.WhenAll(tasks);
        return completedTasks.Where(x => x.BaseBet is not null).ToList();
    }

    private async Task<IEnumerable<PlayerBetType>> GetOutsideBetNumbersAsync(List<PlayerBet> players, CancellationToken token)
    {
        using var tokenSource = new CancellationTokenSource();
        using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(tokenSource.Token, token);

        var tasks = players.Select(async player =>
        {
            var result = await player.Player.Client.PromptClientInput(
                [
                    _rouletteTrans.Prefix(_rouletteTrans.OutsideBetSelected),
                    _rouletteTrans.Prefix(_rouletteTrans.OutsideSelectBet),
                    _rouletteTrans.Prefix(_rouletteTrans.OutsideBetOptions)
                ],
                input =>
                {
                    var parser = new RouletteOutsideBetParser(player.Stake!.Value, _rouletteTrans);
                    var parseResult = parser.Parse(input);
                    var innerResult = new ParsedInputResult<OutsideBaseBet?>();
                    if (!parseResult.IsValid)
                    {
                        return Task.FromResult(innerResult.WithError(parseResult.ErrorMessage));
                    }

                    innerResult.Result = parseResult.Result;
                    return Task.FromResult(innerResult);
                },
                _rouletteTrans.Prefix(_rouletteTrans.InputTimeout),
                linkedTokenSource.Token);

            if (players.Count is not 1 && result.Result is not null)
            {
                player.Player.Client.Tell(_rouletteTrans.Prefix(_rouletteTrans.BetAccepted));
            }

            return new PlayerBetType(player.Player, result.Result);
        });

        var completedTasks = await Task.WhenAll(tasks);
        return completedTasks.Where(x => x.BaseBet is not null).ToList();
    }

    #endregion
}

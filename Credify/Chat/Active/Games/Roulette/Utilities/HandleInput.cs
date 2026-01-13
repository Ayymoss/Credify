using Credify.Chat.Active.Games.Roulette.Enums;
using Credify.Chat.Active.Games.Roulette.Models;
using Credify.Chat.Active.Games.Roulette.Models.BetTypes;
using Credify.Chat.Active.Games.Roulette.Services;
using Credify.Configuration;
using Credify.Configuration.Translations;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Helpers;

// ReSharper disable AccessToDisposedClosure

namespace Credify.Chat.Active.Games.Roulette.Utilities;

public class HandleInput(PersistenceService persistenceService, CredifyConfiguration config)
{
    private readonly RouletteTranslations _rouletteTrans = config.Translations.Roulette;

    private record PlayerBet(Player Player, int? Stake);

    private record PlayerBetCategory(Player Player, BetCategory? BetCategory);

    public record PlayerBetType(Player Player, BaseBet? BaseBet);

    public async Task<List<PlayerBetType>> GetPlayerInputsAsync(List<Player> players, CancellationToken token)
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
            var result = await player.Client.PromptClientInput(
                [_rouletteTrans.Prefix(_rouletteTrans.HowMuchToBet.FormatExt(playerCredits.ToString("N0")))], input =>
                {
                    var innerResult = new ParsedInputResult<int?>();
                    if (!int.TryParse(input, out var innerBet))
                        return Task.FromResult(innerResult.WithError(_rouletteTrans.Prefix(_rouletteTrans.InvalidBetInput)));

                    if (innerBet < 10) return Task.FromResult(innerResult.WithError(_rouletteTrans.Prefix(_rouletteTrans.MinimumBet)));

                    if (innerBet > playerCredits)
                        return Task.FromResult(innerResult.WithError(_rouletteTrans.Prefix(config.Translations.Core.InsufficientCredits)));

                    innerResult.Result = innerBet;
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
            var result = await player.Client.PromptClientInput(
            [
                _rouletteTrans.Prefix(_rouletteTrans.InnerOrOutsideBet),
                _rouletteTrans.Prefix(_rouletteTrans.InnerOrOutsideBetAcceptableInputs)
            ], input =>
            {
                var innerResult = new ParsedInputResult<BetCategory?>();
                var parsed = input.ToLower();
                innerResult.Result = parsed switch
                {
                    "o" or "out" or "outside" => BetCategory.Outside,
                    "i" or "in" or "inside" => BetCategory.Inside,
                    _ => BetCategory.Unknown
                };

                return Task.FromResult(innerResult.Result is BetCategory.Unknown
                    ? innerResult.WithError(_rouletteTrans.Prefix(_rouletteTrans.InvalidBetCategory))
                    : innerResult);
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
                input => Task.FromResult(ParseInsideBet(input, player.Stake!.Value)), _rouletteTrans.Prefix(_rouletteTrans.InputTimeout),
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
                input => Task.FromResult(ParseOutsideBet(input, player.Stake!.Value)),
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

    #region Validation

    private ParsedInputResult<InsideBaseBet?> ParseInsideBet(string input, int betValue)
    {
        var result = new ParsedInputResult<InsideBaseBet?>();
        var (isValid, bet, errorMessage) = RouletteBetValidator.ValidateInsideBet(input, betValue);
        
        if (!isValid)
        {
            return errorMessage switch
            {
                "Invalid number of arguments for inside bet" => result.WithError(_rouletteTrans.Prefix(_rouletteTrans.InvalidNumberOfArguments)),
                "Invalid range or duplicate numbers" => result.WithError(_rouletteTrans.Prefix(_rouletteTrans.InvalidRangeOrDuplicateNumbers)),
                _ => result.WithError(_rouletteTrans.Prefix(_rouletteTrans.InvalidBetType))
            };
        }

        result.Result = bet;
        return result;
    }

    private ParsedInputResult<OutsideBaseBet?> ParseOutsideBet(string input, int betValue)
    {
        var result = new ParsedInputResult<OutsideBaseBet?>();
        var (isValid, bet, errorMessage) = RouletteBetValidator.ValidateOutsideBet(input, betValue);
        
        if (!isValid)
        {
            return errorMessage switch
            {
                "Invalid number of arguments for outside bet" => result.WithError(_rouletteTrans.Prefix(_rouletteTrans.InvalidNumberOfArguments)),
                _ => result.WithError(_rouletteTrans.Prefix(_rouletteTrans.InvalidBetType))
            };
        }

        result.Result = bet;
        return result;
    }

    #endregion
}

using Credify.Chat.Active.Roulette.Enums;
using Credify.Chat.Active.Roulette.Models;
using Credify.Chat.Active.Roulette.Models.BetTypes;
using Credify.Chat.Active.Roulette.Models.BetTypes.Inside;
using Credify.Chat.Active.Roulette.Models.BetTypes.Outside;
using Credify.Configuration;
using Credify.Configuration.Translations;
using SharedLibraryCore;
using SharedLibraryCore.Helpers;

// ReSharper disable AccessToDisposedClosure

namespace Credify.Chat.Active.Roulette.Utilities;

public class HandleInput(PersistenceManager persistenceManager, CredifyConfiguration config)
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
            var playerCredits = await persistenceManager.GetClientCreditsAsync(player.Client);
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
        var argsSplit = input.Split(' ');
        if (argsSplit.Length is < 1 or > 4) return result.WithError(_rouletteTrans.Prefix(_rouletteTrans.InvalidNumberOfArguments));

        var numbers = argsSplit.Select(int.Parse).ToList();
        if (numbers.Any(n => n is < 0 or > 36) || numbers.Distinct().Count() != numbers.Count)
            return result.WithError(_rouletteTrans.Prefix(_rouletteTrans.InvalidRangeOrDuplicateNumbers));

        result.Result = numbers.Count switch
        {
            1 => new StraightUpBet(betValue, numbers[0]),
            2 => IsValidSplit(numbers) ? new SplitBet(betValue, numbers[0], numbers[1]) : null,
            3 => IsValidStreet(numbers) ? new StreetBet(betValue, numbers[0], numbers[1], numbers[2]) : null,
            4 => IsValidCorner(numbers) ? new CornerBet(betValue, numbers[0], numbers[1], numbers[2], numbers[3]) : null,
            6 => IsValidSixLine(numbers)
                ? new SixLineBet(betValue, numbers[0], numbers[1], numbers[2], numbers[3], numbers[4], numbers[5])
                : null,
            _ => null
        };

        return result.Result is null ? result.WithError(_rouletteTrans.Prefix(_rouletteTrans.InvalidBetType)) : result;
    }

    private ParsedInputResult<OutsideBaseBet?> ParseOutsideBet(string input, int betValue)
    {
        var result = new ParsedInputResult<OutsideBaseBet?>();
        var argsSplit = input.Split(' ');
        if (argsSplit.Length is not 1) return result.WithError(_rouletteTrans.Prefix(_rouletteTrans.InvalidNumberOfArguments));
        var rawBetType = argsSplit.First().ToLower();

        result.Result = rawBetType switch
        {
            "r" or "red" => new RedBlackBet(betValue, Colour.Red),
            "b" or "black" => new RedBlackBet(betValue, Colour.Black),
            "o" or "odd" => new OddEvenBet(betValue, false),
            "e" or "even" => new OddEvenBet(betValue, true),
            "l" or "low" => new LowHighBet(betValue, LowHigh.Low),
            "h" or "high" => new LowHighBet(betValue, LowHigh.High),
            "d1" or "dozen1" => new DozenBet(betValue, Dozen.First),
            "d2" or "dozen2" => new DozenBet(betValue, Dozen.Second),
            "d3" or "dozen3" => new DozenBet(betValue, Dozen.Third),
            "c1" or "column1" => new ColumnBet(betValue, Column.First),
            "c2" or "column2" => new ColumnBet(betValue, Column.Second),
            "c3" or "column3" => new ColumnBet(betValue, Column.Third),
            _ => null
        };

        return result.Result is null ? result.WithError(_rouletteTrans.Prefix(_rouletteTrans.InvalidBetType)) : result;
    }

    private static bool IsValidSplit(List<int> numbers)
    {
        if (numbers.Count is not 2) return false;

        numbers.Sort();

        var diff = numbers[1] - numbers[0];

        switch (diff)
        {
            // Horizontal adjacency (same row)
            case 1 when numbers[0] % 3 != 0:
            // Vertical adjacency (same column, except for 0/00)
            case 3 when numbers[0] != 0 && numbers[1] != 36:
                return true;
            default:
                return false;
        }
    }

    private static bool IsValidStreet(List<int> numbers)
    {
        if (numbers.Count is not 3) return false;

        numbers.Sort();

        // Check for consecutive numbers on the same row (difference of 1 between each)
        return numbers[1] - numbers[0] == 1 &&
               numbers[2] - numbers[1] == 1 &&
               numbers[0] % 3 == 1; // Starts at the beginning of a row (1, 4, 7, ...)
    }

    private static bool IsValidCorner(List<int> numbers)
    {
        if (numbers.Count is not 4) return false;

        numbers.Sort();

        // Check if numbers form a 2x2 square on the roulette layout
        return numbers[1] - numbers[0] == 1 && // First row horizontal adjacency
               numbers[3] - numbers[2] == 1 && // Second row horizontal adjacency
               numbers[2] - numbers[0] == 3 && // First column vertical adjacency
               numbers[3] - numbers[1] == 3; // Second column vertical adjacency
    }

    private static bool IsValidSixLine(List<int> numbers)
    {
        if (numbers.Count is not 6) return false;

        numbers.Sort();

        // Check if numbers form two consecutive Streets on the same two rows
        return numbers[1] - numbers[0] == 1 &&
               numbers[2] - numbers[1] == 1 &&
               numbers[3] - numbers[2] == 1 &&
               numbers[4] - numbers[3] == 1 &&
               numbers[5] - numbers[4] == 1 &&
               numbers[0] % 3 == 1 && // Starts at the beginning of a row (1, 4, 7, ...)
               numbers[3] - numbers[0] == 3; // Vertical adjacency between rows
    }

    #endregion
}

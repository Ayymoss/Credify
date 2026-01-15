using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.Roulette.Models.BetTypes;
using Credify.Chat.Active.Games.Roulette.Models.BetTypes.Inside;
using Credify.Chat.Active.Games.Roulette.Services;
using Credify.Configuration.Translations;

namespace Credify.Chat.Active.Games.Roulette.Utilities;

/// <summary>
/// Parser for roulette inside bets. Requires bet value for validation.
/// </summary>
public class RouletteInsideBetParser(int betValue, RouletteTranslations translations)
    : IGameInputParser<InsideBaseBet?>
{
    public ParseResult<InsideBaseBet?> Parse(string input)
    {
        var (isValid, bet, errorMessage) = RouletteBetValidator.ValidateInsideBet(input, betValue);
        
        if (!isValid)
        {
            return errorMessage switch
            {
                "Invalid number of arguments for inside bet" => ParseResult<InsideBaseBet?>.Error(
                    translations.Prefix(translations.InvalidNumberOfArguments)),
                "Invalid range or duplicate numbers" => ParseResult<InsideBaseBet?>.Error(
                    translations.Prefix(translations.InvalidRangeOrDuplicateNumbers)),
                _ => ParseResult<InsideBaseBet?>.Error(
                    translations.Prefix(translations.InvalidBetType))
            };
        }

        return ParseResult<InsideBaseBet?>.Success(bet);
    }
}

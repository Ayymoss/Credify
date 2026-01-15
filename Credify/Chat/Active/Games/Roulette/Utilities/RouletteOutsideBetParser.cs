using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.Roulette.Models.BetTypes;
using Credify.Chat.Active.Games.Roulette.Services;
using Credify.Configuration.Translations;

namespace Credify.Chat.Active.Games.Roulette.Utilities;

/// <summary>
/// Parser for roulette outside bets. Requires bet value for validation.
/// </summary>
public class RouletteOutsideBetParser(int betValue, RouletteTranslations translations)
    : IGameInputParser<OutsideBaseBet?>
{
    public ParseResult<OutsideBaseBet?> Parse(string input)
    {
        var (isValid, bet, errorMessage) = RouletteBetValidator.ValidateOutsideBet(input, betValue);
        
        if (!isValid)
        {
            return errorMessage switch
            {
                "Invalid number of arguments for outside bet" => ParseResult<OutsideBaseBet?>.Error(
                    translations.Prefix(translations.InvalidNumberOfArguments)),
                _ => ParseResult<OutsideBaseBet?>.Error(
                    translations.Prefix(translations.InvalidBetType))
            };
        }

        return ParseResult<OutsideBaseBet?>.Success(bet);
    }
}

using Credify.Chat.Active.Core;
using Credify.Chat.Active.Core.Interfaces;
using Credify.Configuration;
using Credify.Configuration.Translations;

namespace Credify.Chat.Active.Games.Roulette.Utilities;

/// <summary>
/// Parser for roulette stake amounts. Requires player credits for validation.
/// </summary>
public class RouletteStakeParser(
    StakeValidator stakeValidator,
    long playerCredits,
    RouletteTranslations translations,
    CredifyConfiguration config)
    : IGameInputParser<long>
{
    public ParseResult<long> Parse(string message)
    {
        return stakeValidator.ValidateStake(
            message,
            playerCredits,
            translations.Prefix(config.Translations.Core.InsufficientCredits),
            translations.Prefix(translations.MinimumBet),
            translations.Prefix(translations.InvalidBetInput));
    }
}

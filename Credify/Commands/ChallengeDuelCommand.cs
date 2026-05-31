using Credify.Chat.Feature.Duel;
using Credify.Commands.Attributes;
using Credify.Configuration;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

[CommandCategory("Games")]
public class ChallengeDuelCommand : Command
{
    private readonly CredifyConfiguration _credifyConfig;
    private readonly DuelManager _duelManager;

    public ChallengeDuelCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        CredifyConfiguration credifyConfig, DuelManager duelManager) : base(config, translationLookup)
    {
        _credifyConfig = credifyConfig;
        _duelManager = duelManager;
        Name = "credifyduel";
        Alias = "crduel";
        Description = credifyConfig.Translations.Duel.Description;
        Permission = Data.Models.Client.EFClient.Permission.User;
        RequiresTarget = true;
        Arguments =
        [
            new CommandArgument { Name = "Player", Required = true },
            new CommandArgument { Name = "Amount", Required = true }
        ];
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        if (!_credifyConfig.Duel.IsEnabled)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Duel.Disabled);
            return;
        }

        if (gameEvent.Target is null)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Economy.ErrorFindingTargetUser);
            return;
        }

        if (gameEvent.Target.ClientId == gameEvent.Origin.ClientId)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Duel.CannotSelf);
            return;
        }

        if (!long.TryParse(gameEvent.Data.Split(' ').Last(), out var amount))
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.ErrorParsingArgument);
            return;
        }

        await _duelManager.ChallengeAsync(gameEvent.Origin, gameEvent.Target, amount);
    }
}

using Credify.Chat.Feature.Duel;
using Credify.Commands.Attributes;
using Credify.Configuration;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using EFClient = Data.Models.Client.EFClient;

namespace Credify.Commands;

[CommandCategory("Games")]
public class AcceptDuelCommand : Command
{
    private readonly CredifyConfiguration _credifyConfig;
    private readonly DuelManager _duelManager;

    public AcceptDuelCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        CredifyConfiguration credifyConfig, DuelManager duelManager) : base(config, translationLookup)
    {
        _credifyConfig = credifyConfig;
        _duelManager = duelManager;
        Name = "credifyduelaccept";
        Alias = "crduelaccept";
        Description = credifyConfig.Translations.Duel.AcceptDescription;
        Permission = EFClient.Permission.User;
        RequiresTarget = false;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        if (!_credifyConfig.Duel.IsEnabled)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Duel.Disabled);
            return;
        }

        await _duelManager.AcceptAsync(gameEvent.Origin);
    }
}

using Data.Models.Client;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

public class ClaimBetsCommand : Command
{
    private readonly BetManager _betManager;
    private readonly CredifyConfiguration _credifyConfig;

    public ClaimBetsCommand(CommandConfiguration config, ITranslationLookup translationLookup, BetManager betManager,
        CredifyConfiguration credifyConfig) :
        base(config, translationLookup)
    {
        _betManager = betManager;
        _credifyConfig = credifyConfig;
        Name = "claimbets";
        Description = credifyConfig.Translations.CommandClaimCompletedBetsDescription;
        Alias = "cb";
        Permission = EFClient.Permission.User;
        RequiresTarget = false;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        // If bets have been made, return the expired bet to the completed player.
        var completedMessages = _betManager.CompletedBetsMessages(gameEvent.Origin);

        if (completedMessages is not null && completedMessages.Any())
        {
            await gameEvent.Origin.TellAsync(completedMessages);
            _betManager.RemoveCompletedBets(gameEvent.Origin);
            return;
        }

        gameEvent.Origin.Tell(_credifyConfig.Translations.NoCompletedBetsToClaim);
    }
}

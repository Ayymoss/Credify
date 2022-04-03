using Data.Models.Client;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace CreditsPlugin.Commands;

public class ClaimBetsCommand : Command
{
    public ClaimBetsCommand(CommandConfiguration config, ITranslationLookup translationLookup) :
        base(config, translationLookup)
    {
        Name = "claimbets";
        Description = "Claims your completed bets";
        Alias = "cb";
        Permission = EFClient.Permission.User;
        RequiresTarget = false;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        if (gameEvent.Type != GameEvent.EventType.Command) return;

        // If bets have been made, return the expired bet to the completed player.
        var completedMessages = Plugin.BetManager.CompletedBetsMessages(gameEvent.Origin);

        if (completedMessages is not null && completedMessages.Any())
        {
            await gameEvent.Origin.TellAsync(completedMessages);
            Plugin.BetManager.RemoveCompletedBets(gameEvent.Origin);
            return;
        }

        gameEvent.Origin.Tell("(Color::Yellow)You have no completed bets to claim");
    }
}

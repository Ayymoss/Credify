using Data.Models.Client;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace CreditsPlugin.Commands;

public class CancelBetsCommand : Command
{
    public CancelBetsCommand(CommandConfiguration config, ITranslationLookup translationLookup) :
        base(config, translationLookup)
    {
        Name = "cancelbets";
        Description = "Cancel your open bets";
        Alias = "cb";
        Permission = EFClient.Permission.User;
        RequiresTarget = false;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        if (gameEvent.Type != GameEvent.EventType.Command) return;

        //if (!Plugin.BetManager.MaximumTimePassed(gameEvent.Origin))
        //{
        //    gameEvent.Origin.Tell(
        //        $"(Color::Yellow)Bets only accepted during first {Plugin.CreditsMaximumBetTime} minutes");
        //    return;
        //}

        var cancelledBets = Plugin.BetManager.CancelBets(gameEvent.Origin);
        if (cancelledBets == 0)
        {
            gameEvent.Origin.Tell($"(Color::Yellow)You have no bets to cancel");
            return;
        }

        gameEvent.Origin.Tell($"You bets ({cancelledBets}) have been cancelled");
    }
}

using CreditsPlugin;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

public class BetsCommand : Command
{
    public BetsCommand(CommandConfiguration config, ITranslationLookup translationLookup) :
        base(config, translationLookup)
    {
        Name = "openbets";
        Description = "Lists all open bets";
        Alias = "ob";
        Permission = EFClient.Permission.User;
        RequiresTarget = false;
    }

    public async override Task ExecuteAsync(GameEvent gameEvent)
    {
        if (gameEvent.Type != GameEvent.EventType.Command) return;
        Plugin.BetManager?.GetOpenBets(gameEvent);
    }
}

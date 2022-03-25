using CreditsPlugin;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

public class OpenBetsCommand : Command
{
    public OpenBetsCommand(CommandConfiguration config, ITranslationLookup translationLookup) :
        base(config, translationLookup)
    {
        Name = "openbets";
        Description = "Lists all open bets";
        Alias = "ob";
        Permission = EFClient.Permission.User;
        RequiresTarget = false;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        var openBets = Plugin.BetManager.GetOpenBets();
        if (!openBets.Any())
        {
            gameEvent.Origin.Tell("(Color::Yellow)There are no open bets");
            return;
        }

        gameEvent.Origin.Tell("(Color::Cyan)--Open Bets--");
        await gameEvent.Origin.TellAsync(openBets.Select((value, i) =>
            $"#(Color::Cyan){i + 1} (Color::White)- (Color::Green){value.Origin.CleanedName} (Color::White)- (Color::Red){value.Target.CleanedName} (Color::White)- (Color::Cyan){value.InitAmount:N0}")
        );
    }
}

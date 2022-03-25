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

        string target = null;
        var index = 0;
        var stringList = new List<string>();
        foreach (var bet in openBets)
        {
            if (bet.Team == EFClient.TeamType.Allies) target = "Allies";
            if (bet.Team == EFClient.TeamType.Axis) target = "Axis";
            if (bet.TargetPlayer != null) target = bet.TargetPlayer.CleanedName;

            stringList.Add($"#(Color::Cyan){index + 1} (Color::White)- (Color::Green){bet.Origin.CleanedName} " +
                           $"(Color::White)- (Color::Red){target} (Color::White)- (Color::Cyan){bet.InitAmount:N0}");
            index++;
        }

        await gameEvent.Origin.TellAsync(stringList);
    }
}

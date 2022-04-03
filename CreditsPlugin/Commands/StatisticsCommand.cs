using Data.Models.Client;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace CreditsPlugin.Commands;

public class StatisticsCommand : Command
{
    public StatisticsCommand(CommandConfiguration config, ITranslationLookup translationLookup) :
        base(config, translationLookup)
    {
        Name = "creditstats";
        Description = "Check your credits.";
        Alias = "statscr";
        Permission = EFClient.Permission.User;
        RequiresTarget = false;
    }

    public override Task ExecuteAsync(GameEvent gameEvent)
    {
        if (gameEvent.Type != GameEvent.EventType.Command) return Task.CompletedTask;

        gameEvent.Origin.TellAsync(new[]
        {
            "(Color::Cyan)--Credit Statistics--",
            $"Total Earned: (Color::Cyan){Plugin.PrimaryLogic.StatisticsState.CreditsEarned:N0} (Color::White)credits",
            $"Total Spent: (Color::Cyan){Plugin.PrimaryLogic.StatisticsState.CreditsSpent:N0} (Color::White)credits",
            $"Total Paid: (Color::Cyan){Plugin.PrimaryLogic.StatisticsState.CreditsPaid:N0} (Color::White)credits"
        });

        return Task.CompletedTask;
    }
}

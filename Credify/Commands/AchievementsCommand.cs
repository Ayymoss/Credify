using Credify.Chat.Feature.Achievements;
using Credify.Commands.Attributes;
using Credify.Configuration;
using Credify.Helpers;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using EFClient = Data.Models.Client.EFClient;

namespace Credify.Commands;

[CommandCategory("Credits")]
public class AchievementsCommand : Command
{
    private readonly CredifyConfiguration _credifyConfig;
    private readonly AchievementManager _achievementManager;

    public AchievementsCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        CredifyConfiguration credifyConfig, AchievementManager achievementManager) : base(config, translationLookup)
    {
        _credifyConfig = credifyConfig;
        _achievementManager = achievementManager;
        Name = "credifyachievements";
        Alias = "crach";
        Description = credifyConfig.Translations.Achievements.Description;
        Permission = EFClient.Permission.User;
        RequiresTarget = false;
    }

    public override Task ExecuteAsync(GameEvent gameEvent)
    {
        var achievements = _credifyConfig.Translations.Achievements;
        if (!_credifyConfig.Achievement.IsEnabled)
        {
            gameEvent.Origin.Tell(achievements.Disabled);
            return Task.CompletedTask;
        }

        var all = _credifyConfig.Achievement.Achievements.Where(a => a.Enabled).ToList();
        var progress = _achievementManager.GetProgress(gameEvent.Origin);
        var unlocked = all.Where(a => progress.Unlocked.Contains(a.Id)).ToList();
        var locked = all.Where(a => !progress.Unlocked.Contains(a.Id)).ToList();

        const string separator = " (Color::White)| ";
        const int maxWidth = 56;

        List<string> lines = [achievements.Header.FormatExt(unlocked.Count, all.Count)];

        if (unlocked.Count > 0)
            lines.AddRange(ChatLines.Pack(achievements.UnlockedLabel, unlocked.Select(a => a.Name).ToList(), separator, maxWidth));

        // Next-up: the unearned achievements closest to their threshold.
        if (locked.Count > 0)
        {
            var inProgress = locked
                .OrderByDescending(a => ProgressOf(progress, a) / (double)a.Threshold)
                .Take(3)
                .Select(a => achievements.ProgressEntry.FormatExt(
                    a.Name,
                    Math.Min(ProgressOf(progress, a), a.Threshold).ToString("N0"),
                    a.Threshold.ToString("N0")))
                .ToList();
            lines.AddRange(ChatLines.Pack(achievements.InProgressLabel, inProgress, separator, maxWidth));
        }

        if (lines.Count > 5) lines = lines.Take(5).ToList();
        return gameEvent.Origin.TellAsync(lines);
    }

    private static long ProgressOf(Chat.Feature.Achievements.Models.AchievementProgress progress, Achievement achievement)
    {
        progress.Totals.TryGetValue((int)achievement.Objective, out var total);
        return total;
    }
}

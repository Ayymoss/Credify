using System.Text.Json;
using Credify.Chat.Passive.Quests;
using Credify.Chat.Passive.Quests.Models;
using Credify.Commands.Attributes;
using Credify.Configuration;
using Credify.Helpers;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

[CommandCategory("Quests")]
public class QuestCommand : Command
{
    private readonly CredifyConfiguration _credifyConfig;
    private readonly QuestManager _questManager;

    public QuestCommand(CredifyConfiguration credifyConfig, QuestManager questManager, CommandConfiguration config,
        ITranslationLookup layout) : base(config, layout)
    {
        _credifyConfig = credifyConfig;
        _questManager = questManager;
        Name = "credifyquests";
        Description = credifyConfig.Translations.Quests.Description;
        Alias = "crq";
        Permission = EFClient.Permission.User;
        RequiresTarget = false;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        var playerQuests = _questManager.GetPlayerQuests(gameEvent.Origin);

        // In-progress permanents first (most relevant), completed ones hidden to de-clutter.
        var permanentQuests = playerQuests
            .Where(x => !x.Completed)
            .Where(quest => _questManager.ActiveQuests.Any(aq => aq.IsPermanent && (int)aq.ObjectiveType == quest.QuestId))
            .OrderByDescending(q => q.Progress)
            .Select(QuestMessage)
            .ToList();

        var dailyQuests = playerQuests
            .Where(quest => _questManager.ActiveQuests.Any(aq => !aq.IsPermanent && (int)aq.ObjectiveType == quest.QuestId))
            .Select(QuestMessage)
            .ToList();

        var trans = _credifyConfig.Translations.Quests;
        const string separator = " (Color::White)| ";
        const int maxWidth = 56;

        List<string> messages = [];
        if (dailyQuests.Count is not 0)
            messages.AddRange(ChatLines.Pack(trans.DailyLabel, dailyQuests, separator, maxWidth));
        if (permanentQuests.Count is not 0)
            messages.AddRange(ChatLines.Pack(trans.PermanentLabel, permanentQuests, separator, maxWidth));

        if (messages.Count is 0)
        {
            messages.Add(trans.NoQuests);
        }
        else if (messages.Count > 5)
        {
            // Keep within the chat line budget; surface the overflow instead of spamming.
            messages = messages.Take(4).Append(trans.MoreQuests).ToList();
        }

        await gameEvent.Origin.TellAsync(messages);
    }

    private string QuestMessage(QuestMeta questMeta)
    {
        var activeQuest = _questManager.ActiveQuests.First(aq => (int)aq.ObjectiveType == questMeta.QuestId);

        var progressMessage = questMeta.Progress == activeQuest.ObjectiveCount
            ? _credifyConfig.Translations.Quests.Completed
            : _credifyConfig.Translations.Quests.ProgressFormat
                .FormatExt(questMeta.Progress.ToString("N0"), activeQuest.ObjectiveCount.ToString("N0"));

        return _credifyConfig.Translations.Quests.Quest.FormatExt(activeQuest.Name, progressMessage);
    }
}

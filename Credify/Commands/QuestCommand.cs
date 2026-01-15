using System.Text.Json;
using Credify.Chat.Passive.Quests;
using Credify.Chat.Passive.Quests.Models;
using Credify.Commands.Attributes;
using Credify.Configuration;
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
        Description = credifyConfig.Translations.Core.CommandQuestDescription;
        Alias = "crq";
        Permission = EFClient.Permission.User;
        RequiresTarget = false;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        var playerQuests = _questManager.GetPlayerQuests(gameEvent.Origin);

        var permanentQuests = playerQuests
            .Where(x => !x.Completed) // Remove completed perms to de-clutter view.
            .Where(quest => _questManager.ActiveQuests.Any(aq => aq.IsPermanent && (int)aq.ObjectiveType == quest.QuestId))
            .Select(QuestMessage)
            .OrderBy(_ => Guid.NewGuid())
            .ToList();

        var dailyQuests = playerQuests
            .Where(quest => _questManager.ActiveQuests.Any(aq => !aq.IsPermanent && (int)aq.ObjectiveType == quest.QuestId))
            .Select(QuestMessage)
            .ToList();

        List<string> messages = [];
        if (permanentQuests.Count is not 0)
        {
            messages.Add(_credifyConfig.Translations.Quests.PermanentHeader);
            messages.AddRange(permanentQuests);
        }

        if (dailyQuests.Count is not 0)
        {
            messages.Add(_credifyConfig.Translations.Quests.DailyHeader);
            messages.AddRange(dailyQuests);
        }

        if (messages.Count is 0)
        {
            messages.Add(_credifyConfig.Translations.Quests.NoQuests);
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

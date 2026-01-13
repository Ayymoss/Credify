using System.Text.RegularExpressions;
using Credify.Chat.Passive.Quests.Enums;
using Credify.Chat.Passive.Quests.Models;
using Credify.Configuration;
using Credify.Constants;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Events.Game;

namespace Credify.Chat.Passive.Quests;

public class QuestManager(CredifyConfiguration config, PersistenceService persistenceService)
{
    public List<Quest> ActiveQuests { get; private set; } = [];
    private readonly SemaphoreSlim _semaphoreSlim = new(1, 1);

    #region Core

    public List<QuestMeta> GetPlayerQuests(EFClient client)
    {
        if (ActiveQuests.Count is 0) return [];

        var clientQuests = client.GetAdditionalProperty<List<QuestMeta>>(PluginConstants.ClientQuestsKey) ?? [];

        var missingQuestIds = ActiveQuests
            .Select(q => (int)q.ObjectiveType)
            .Except(clientQuests.Select(cq => cq.QuestId))
            .ToList();

        clientQuests.AddRange(missingQuestIds.Select(questId => new QuestMeta { QuestId = questId, Progress = 0, Completed = false }));

        // Update daily quests so that they don't show previous values. Kinda hacky. Meh.
        foreach (var quest in clientQuests)
        {
            if (quest is not { Completed: true, CompletedDay: not null } ||
                quest.CompletedDay == TimeProvider.System.GetLocalNow().Day) continue;

            quest.Completed = false;
            quest.Progress = 0;
            quest.CompletedDay = null;
        }

        client.SetAdditionalProperty(PluginConstants.ClientQuestsKey, clientQuests);
        return clientQuests;
    }

    public void GenerateDailyQuests()
    {
        ActiveQuests.Clear();
        var dailyQuests = config.Quest.Quests
            .Where(q => q.Enabled)
            .Where(x => !x.IsPermanent)
            .ToList();

        if (dailyQuests.Count <= 3)
        {
            ActiveQuests = dailyQuests;
            return;
        }

        var selectedIndices = new HashSet<int>();
        while (selectedIndices.Count < 3)
        {
            int randomIndex;
            do
            {
                randomIndex = Random.Shared.Next(dailyQuests.Count);
            } while (selectedIndices.Contains(randomIndex));

            selectedIndices.Add(randomIndex);
            ActiveQuests.Add(dailyQuests[randomIndex]);
        }

        var permQuests = config.Quest.Quests.Where(x => x.IsPermanent);
        ActiveQuests = ActiveQuests.Concat(permQuests).ToList();
    }

    private void UpdatePlayerProgress(EFClient client, int questId, int increment)
    {
        if (client.ClientId is 0) return;

        var clientQuests = GetPlayerQuests(client);
        var questMeta = clientQuests.FirstOrDefault(x => x.QuestId == questId);

        if (questMeta is null) return;

        // If it's a daily quest (which will have a CompletedDay), let's reset the quest progress for the next day.
        if (questMeta is { Completed: true, CompletedDay: not null } && questMeta.CompletedDay != TimeProvider.System.GetLocalNow().Day)
        {
            questMeta.Completed = false;
            questMeta.Progress = 0;
            questMeta.CompletedDay = null;
        }

        if (questMeta.Completed) return;

        questMeta.Progress += increment;
        client.SetAdditionalProperty(PluginConstants.ClientQuestsKey, clientQuests);
    }

    private async Task CheckQuestCompletionAsync(EFClient client, Quest quest)
    {
        if (client.ClientId is 0) return;

        var playerQuests = GetPlayerQuests(client);
        var questMeta = playerQuests.FirstOrDefault(q => q.QuestId == (int)quest.ObjectiveType);
        if (questMeta is null) return;

        var isCompleted = questMeta.Progress >= quest.ObjectiveCount;
        if (!isCompleted || questMeta.Completed) return;

        await persistenceService.AddCreditsAsync(client, quest.Reward);
        client.CurrentServer.Broadcast(config.Translations.Quests.CompletedQuest
            .FormatExt(client.CleanedName, quest.Name, quest.Reward.ToString("N0")));

        if (!quest.IsPermanent) questMeta.CompletedDay = TimeProvider.System.GetLocalNow().Day;
        questMeta.Completed = true;

        if (quest.IsRepeatable)
        {
            questMeta.Completed = false;
            questMeta.Progress = 0;
            questMeta.CompletedDay = null;
        }

        client.SetAdditionalProperty(PluginConstants.ClientQuestsKey, playerQuests);
    }

    #endregion

    #region Handlers

    public async Task HandleKillAsync(ClientKillEvent killEvent)
    {
        var weapon = config.Quest.WeaponLookupTable
            .Where(x => x.Value.Any(pattern => Regex.IsMatch(killEvent.WeaponName, pattern)))
            .Select(x => x.Key)
            .FirstOrDefault(Weapon.Unknown);

        var meansOfDeath = config.Quest.MeansOfDeathLookupTable
            .Where(x => x.Value.Any(pattern => Regex.IsMatch(killEvent.MeansOfDeath, pattern)))
            .Select(x => x.Key)
            .FirstOrDefault(MeansOfDeath.Unknown);

        var origin = killEvent.Origin;
        var target = killEvent.Target;
#if DEBUG
        Console.WriteLine($"{origin.CleanedName} (@{origin.ClientId}) -> " +
                          $"{target.CleanedName} (@{target.ClientId}) | " +
                          $"MOD: {meansOfDeath.ToString()} ({killEvent.MeansOfDeath}) - " +
                          $"WEP: {weapon.ToString()} ({killEvent.WeaponName})");
#endif
        try
        {
            await _semaphoreSlim.WaitAsync();

            foreach (var quest in ActiveQuests)
            {
                switch (quest.ObjectiveType)
                {
                    case ObjectiveType.Melee when meansOfDeath is MeansOfDeath.Melee && weapon is not Weapon.RiotShield:
                    case ObjectiveType.Headshot when meansOfDeath is MeansOfDeath.Headshot:
                    case ObjectiveType.Suicide when meansOfDeath is MeansOfDeath.Suicide || origin.ClientId == target.ClientId ||
                                                    (origin.ClientId == 0 && weapon is Weapon.Unknown): // Last one is suicide by falling
                    case ObjectiveType.Impact when meansOfDeath is MeansOfDeath.Impact && origin.ClientId != target.ClientId:
                    case ObjectiveType.HotPotato when weapon is Weapon.Grenade && origin.ClientId != target.ClientId:
                    case ObjectiveType.RiotShield when weapon is Weapon.RiotShield:
                    case ObjectiveType.Silenced when weapon is Weapon.Silenced:
                    case ObjectiveType.Kill:
                        UpdatePlayerProgress(origin, (int)quest.ObjectiveType, 1);
                        break;
                    case ObjectiveType.Humiliation when meansOfDeath is MeansOfDeath.Melee && weapon is not Weapon.RiotShield:
                        UpdatePlayerProgress(target, (int)quest.ObjectiveType, 1);
                        break;
                }

                await CheckQuestCompletionAsync(origin, quest);
                await CheckQuestCompletionAsync(target, quest); // For humiliation
            }
        }
        finally
        {
            if (_semaphoreSlim.CurrentCount is 0) _semaphoreSlim.Release();
        }
    }

    public async Task HandleChatAsync(EFClient client, string message)
    {
        try
        {
            await _semaphoreSlim.WaitAsync();

            foreach (var quest in ActiveQuests)
            {
                switch (quest.ObjectiveType)
                {
                    case ObjectiveType.MyNameJeff when message.Equals("my name jeff", StringComparison.CurrentCultureIgnoreCase):
                    case ObjectiveType.Chat:
                        UpdatePlayerProgress(client, (int)quest.ObjectiveType, 1);
                        break;
                }

                await CheckQuestCompletionAsync(client, quest);
            }
        }
        finally
        {
            if (_semaphoreSlim.CurrentCount is 0) _semaphoreSlim.Release();
        }
    }

    public async Task HandleCredifyEvent(ObjectiveType objective, EFClient client, object? data)
    {
        try
        {
            await _semaphoreSlim.WaitAsync();

            var quest = ActiveQuests.FirstOrDefault(q => q.ObjectiveType == objective);
            if (quest is null) return;

            switch (objective)
            {
                case ObjectiveType.Trivia:
                case ObjectiveType.Raffle:
                case ObjectiveType.Blackjack:
                case ObjectiveType.TopHolder:
                case ObjectiveType.Roulette:
                    UpdatePlayerProgress(client, (int)quest.ObjectiveType, 1);
                    break;
                case ObjectiveType.Donation:
                case ObjectiveType.Baller:
                case ObjectiveType.CreditsSpent:
                    if (data is not long longValue) return;

                    var maxCredits = longValue switch
                    {
                        > int.MaxValue => int.MaxValue,
                        >= 0 => (int)longValue,
                        _ => 0
                    };

                    UpdatePlayerProgress(client, (int)quest.ObjectiveType, maxCredits);
                    break;
            }

            await CheckQuestCompletionAsync(client, quest);
        }
        finally
        {
            if (_semaphoreSlim.CurrentCount is 0) _semaphoreSlim.Release();
        }
    }

    #endregion
}

using Credify.Constants;
using Credify.Chat.Passive.Quests.Models;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace Credify.Services;

/// <summary>
/// Service responsible for managing quest data persistence.
/// </summary>
public class QuestPersistenceService(
    IMetaServiceV2 metaService)
{
    /// <summary>
    /// Reads client quests from persistent storage.
    /// </summary>
    public async Task<List<QuestMeta>> ReadClientQuestsAsync(EFClient client)
    {
        var quests = await metaService.GetPersistentMetaValue<List<QuestMeta>>(PluginConstants.ClientQuestsKey, client.ClientId) ?? [];
        client.SetAdditionalProperty(PluginConstants.ClientQuestsKey, quests);
        return quests;
    }

    /// <summary>
    /// Writes client quests to persistent storage.
    /// </summary>
    public async Task WriteClientQuestsAsync(EFClient client)
    {
        var quests = client.GetAdditionalProperty<List<QuestMeta>>(PluginConstants.ClientQuestsKey) ?? [];
        await metaService.SetPersistentMetaValue(PluginConstants.ClientQuestsKey, quests, client.ClientId);
    }

    /// <summary>
    /// Loads client quests on join.
    /// </summary>
    public async Task LoadQuestsOnJoinAsync(EFClient client)
    {
        await ReadClientQuestsAsync(client);
    }
}

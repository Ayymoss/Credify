using Credify.Chat.Active.Roulette.Utilities;
using Credify.Configuration;
using SharedLibraryCore.Database.Models;
using Player = Credify.Chat.Active.Roulette.Models.Player;

namespace Credify.Chat.Active.Roulette;

public class TableManager(CredifyConfiguration config, TranslationsRoot translations, PersistenceManager persistenceManager, HandleInput input, HandleOutput output)
{
    private readonly Table _table = new(config, translations, persistenceManager, input, output);

    public async Task StartGame(CancellationToken token) => await _table.GameLoopAsync(token);
    public async Task<bool> AddPlayerAsync(EFClient client) => await _table.PlayerJoinAsync(new Player(client));
    public void RemovePlayer(EFClient client) => _table.PlayerLeave(client);
    public bool IsPlayerInGame(EFClient client) => _table.IsPlayerInGame(client);
}

using Credify.Chat.Active.Raffle;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

public class CallRaffleCommand : Command
{
    private readonly RaffleManager _raffleManager;

    public CallRaffleCommand(CommandConfiguration config, ITranslationLookup layout, RaffleManager raffleManager) :
        base(config, layout)
    {
        _raffleManager = raffleManager;
        Name = "credifycallraffle";
        Description = "DEBUG - Calls the raffle now";
        Alias = "crcallraffle";
        Permission = Data.Models.Client.EFClient.Permission.Owner;
        RequiresTarget = false;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        await _raffleManager.DrawWinnerAsync();
    }
}

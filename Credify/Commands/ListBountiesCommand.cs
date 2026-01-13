using Credify.Chat.Feature.Bounty;
using Credify.Configuration;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

public class ListBountiesCommand : Command
{
    private readonly CredifyConfiguration _credifyConfig;
    private readonly BountyContractManager _bountyManager;

    public ListBountiesCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        CredifyConfiguration credifyConfig, BountyContractManager bountyManager) : base(config, translationLookup)
    {
        _credifyConfig = credifyConfig;
        _bountyManager = bountyManager;
        Name = "credifybounties";
        Alias = "crbounties";
        Description = credifyConfig.Translations.Core.CommandListBountiesDescription;
        Permission = Data.Models.Client.EFClient.Permission.User;
        RequiresTarget = false;
        Arguments = [];
    }

    public override Task ExecuteAsync(GameEvent gameEvent)
    {
        if (!_credifyConfig.BountyContract.IsEnabled)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.BountyContractDisabled);
            return Task.CompletedTask;
        }

        var bounties = _bountyManager.GetAllActiveBounties();

        if (bounties.Count == 0)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.NoBountiesActive);
            return Task.CompletedTask;
        }

        gameEvent.Origin.Tell(_credifyConfig.Translations.Core.BountiesHeader);

        // Show top 5 bounties
        var topBounties = bounties.Take(5).ToList();
        for (var i = 0; i < topBounties.Count; i++)
        {
            var bounty = topBounties[i];
            var msg = _credifyConfig.Translations.Core.BountyListEntry.FormatExt(
                i + 1,
                bounty.Amount.ToString("N0"),
                bounty.TargetName);
            gameEvent.Origin.Tell(msg);
        }

        if (bounties.Count > 5)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.BountiesMoreCount.FormatExt(bounties.Count - 5));
        }

        return Task.CompletedTask;
    }
}

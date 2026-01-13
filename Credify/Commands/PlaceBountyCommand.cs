using Credify.Chat.Feature.Bounty;
using Credify.Configuration;
using Credify.Constants;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

public class PlaceBountyCommand : Command
{
    private readonly PersistenceService _persistenceService;
    private readonly CredifyConfiguration _credifyConfig;
    private readonly BountyContractManager _bountyManager;

    public PlaceBountyCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        PersistenceService persistenceService, CredifyConfiguration credifyConfig,
        BountyContractManager bountyManager) : base(config, translationLookup)
    {
        _persistenceService = persistenceService;
        _credifyConfig = credifyConfig;
        _bountyManager = bountyManager;
        Name = "credifybounty";
        Alias = "crbounty";
        Description = credifyConfig.Translations.Core.CommandPlaceBountyDescription;
        Permission = Data.Models.Client.EFClient.Permission.User;
        RequiresTarget = true;
        Arguments =
        [
            new CommandArgument
            {
                Name = "Player",
                Required = true
            },
            new CommandArgument
            {
                Name = "Amount",
                Required = true
            }
        ];
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        if (!_credifyConfig.BountyContract.IsEnabled)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.BountyContractDisabled);
            return;
        }

        if (gameEvent.Target is null)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.ErrorFindingTargetUser);
            return;
        }

        if (gameEvent.Target.ClientId == gameEvent.Origin.ClientId)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.CannotTargetSelf);
            return;
        }

        var args = gameEvent.Data.Split(' ');
        if (args.Length < 1)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.ErrorParsingArgument);
            return;
        }

        // Last argument is the amount
        var amountArg = args.Last();
        if (!long.TryParse(amountArg, out var amount))
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.ErrorParsingArgument);
            return;
        }

        var result = await _bountyManager.PlaceBountyAsync(gameEvent.Origin, gameEvent.Target, amount);

        if (!result.Success)
        {
            gameEvent.Origin.Tell($"(Color::Yellow){result.ErrorMessage}");
            return;
        }

        // Notify placer
        var placerMsg = _credifyConfig.Translations.Core.BountyContractPlaced.FormatExt(
            result.Contract!.Amount.ToString("N0"),
            gameEvent.Target.CleanedName,
            result.Fee.ToString("N0"));
        gameEvent.Origin.Tell(placerMsg);

        // Notify target
        var targetMsg = _credifyConfig.Translations.Core.BountyContractTargeted.FormatExt(
            result.Contract.Amount.ToString("N0"),
            gameEvent.Origin.CleanedName);
        gameEvent.Target.Tell(targetMsg);

        // Announce to server if enabled
        if (_credifyConfig.BountyContract.AnnouncePlacement)
        {
            var announceMsg = _credifyConfig.Translations.Core.BountyContractAnnouncement.FormatExt(
                PluginConstants.PluginName,
                result.Contract.Amount.ToString("N0"),
                gameEvent.Target.CleanedName);
            gameEvent.Owner?.Broadcast(announceMsg);
        }
    }
}

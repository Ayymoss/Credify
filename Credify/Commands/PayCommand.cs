using Credify.Chat.Passive.Quests.Enums;
using Credify.Commands.Attributes;
using Credify.Configuration;
using Credify.Models;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using EFClient = Data.Models.Client.EFClient;

namespace Credify.Commands;

[CommandCategory("Credits")]
public class PayCommand : Command
{
    private readonly PersistenceService _persistenceService;
    private readonly CredifyConfiguration _credifyConfig;

    public PayCommand(CommandConfiguration config, ITranslationLookup translationLookup, PersistenceService persistenceService,
        CredifyConfiguration credifyConfig)
        : base(config, translationLookup)
    {
        _persistenceService = persistenceService;
        _credifyConfig = credifyConfig;
        Name = "credifypay";
        Description = credifyConfig.Translations.Core.CommandPayCreditsDescription;
        Alias = "crpay";
        Permission = EFClient.Permission.User;
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
        var argCredits = gameEvent.Data;

        if (!long.TryParse(argCredits, out var credits))
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.ErrorParsingSecondArgument);
            return;
        }

        if (gameEvent.Origin.ClientId == gameEvent.Target.ClientId)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.CannotTargetSelf);
            return;
        }

        if (gameEvent.Target.ClientId is 1)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.CannotTargetConsole);
            return;
        }

        if (credits < 10)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.MinimumAmount);
            return;
        }

        if (credits > _credifyConfig.Core.MaxGiveCredits)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.MaximumAmount.FormatExt(_credifyConfig.Core.MaxGiveCredits));
            return;
        }

        ICredifyEventService.RaiseEvent(ObjectiveType.Donation, gameEvent.Origin, credits);
        await _persistenceService.RemoveCreditsAsync(gameEvent.Origin, credits);
        await _persistenceService.AddCreditsAsync(gameEvent.Target, credits);

        gameEvent.Origin.Tell(_credifyConfig.Translations.Core.PaySent
            .FormatExt(credits.ToString("N0"), gameEvent.Target.CleanedName));
        gameEvent.Target.Tell(_credifyConfig.Translations.Core.PayReceived
            .FormatExt(credits.ToString("N0"), gameEvent.Origin.CleanedName));
    }
}

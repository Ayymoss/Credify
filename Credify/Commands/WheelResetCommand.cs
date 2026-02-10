using Credify.Commands.Attributes;
using Credify.Configuration;
using Credify.Constants;
using Credify.Services;
using Data.Models.Client;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using EFClient = Data.Models.Client.EFClient;

namespace Credify.Commands;

[CommandCategory("Admin")]
public class WheelResetCommand : Command
{
    private readonly IMetaServiceV2 _metaService;
    private readonly CredifyConfiguration _credifyConfig;

    public WheelResetCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        IMetaServiceV2 metaService, CredifyConfiguration credifyConfig) 
        : base(config, translationLookup)
    {
        _metaService = metaService;
        _credifyConfig = credifyConfig;
        Name = "credifywheelreset";
        Alias = "crwofreset";
        Description = "Reset wheel cooldown for a player";
        Permission = EFClient.Permission.Owner;
        RequiresTarget = true;
        Arguments =
        [
            new CommandArgument
            {
                Name = "Player",
                Required = true
            }
        ];
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        // Handle target resolution if not already set
        if (gameEvent.Target == null && !string.IsNullOrWhiteSpace(gameEvent.Data))
        {
            gameEvent.Target = gameEvent.Owner.GetClientByName(gameEvent.Data).FirstOrDefault();
        }

        if (gameEvent.Target == null)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.ErrorFindingTargetUser);
            return;
        }

        // Reset the wheel cooldown by setting to yesterday's date
        // This makes CanSpinWheel return true (lastUsed < today)
        var yesterday = DateTime.Now.Date.AddDays(-1).ToString("yyyy-MM-dd");
        await _metaService.SetPersistentMeta(PluginConstants.WheelLastUsed, yesterday, gameEvent.Target.ClientId);

        gameEvent.Origin.Tell(_credifyConfig.Translations.Core.WheelResetSuccess.FormatExt(gameEvent.Target.CleanedName));
        
        if (gameEvent.Origin.ClientId != gameEvent.Target.ClientId)
        {
            gameEvent.Target.Tell(_credifyConfig.Translations.Core.WheelResetTarget.FormatExt(gameEvent.Origin.CleanedName));
        }
    }
}

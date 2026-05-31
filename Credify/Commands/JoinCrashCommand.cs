using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.Crash;
using Credify.Commands.Attributes;
using Credify.Commands.Base;
using Credify.Configuration;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

[CommandCategory("Games")]
public class JoinCrashCommand : Command
{
    private readonly GameJoinCommandHelper<CrashGame> _helper;
    private readonly CredifyConfiguration _credifyConfig;

    public JoinCrashCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        CrashGame crashGame, CredifyConfiguration credifyConfig,
        PersistenceService persistenceService, ActiveGameTracker gameTracker)
        : base(config, translationLookup)
    {
        _helper = new GameJoinCommandHelper<CrashGame>(crashGame, credifyConfig, persistenceService, gameTracker);
        _credifyConfig = credifyConfig;
        Name = "credifycrash";
        Alias = "crcrash";
        Description = credifyConfig.Translations.Crash.Description;
        Permission = Data.Models.Client.EFClient.Permission.User;
        RequiresTarget = false;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        // Crash relies on game-log latency to time cash-outs fairly. Without it (no GSC
        // companion on this server) cashing would be guesswork, so it's unavailable here.
        if (_credifyConfig.Crash.IsEnabled &&
            gameEvent.Origin.CurrentServer.LatencyMetrics?.GameLogPipelineMs is null)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Crash.NoLatency);
            return;
        }

        await _helper.ExecuteAsync(
            gameEvent,
            isGameEnabled: _credifyConfig.Crash.IsEnabled,
            minimumCredits: GameConstants.MinimumCredits,
            disabledMessage: _credifyConfig.Translations.Crash.Disabled,
            insufficientCreditsMessage: _credifyConfig.Translations.Core.InsufficientCredits,
            handleLeaveSuccessAsync: async ge => { ge.Origin.Tell(_credifyConfig.Translations.Crash.Leave); });
    }
}

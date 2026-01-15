using Credify.Chat.Active.Core;
using Credify.Configuration;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;

namespace Credify.Commands.Base;

/// <summary>
/// Helper class for game join commands that provides common functionality.
/// Used via composition to avoid inheritance issues with command discovery.
/// </summary>
/// <typeparam name="TManager">The game manager type that implements IActiveGame</typeparam>
public class GameJoinCommandHelper<TManager>(
    TManager gameManager,
    CredifyConfiguration credifyConfig,
    PersistenceService persistenceService,
    ActiveGameTracker gameTracker)
    where TManager : IActiveGame
{
    private readonly TManager _gameManager = gameManager;

    /// <summary>
    /// Executes the common join/leave logic for a game command.
    /// </summary>
    public async Task ExecuteAsync(
        GameEvent gameEvent,
        bool isGameEnabled,
        long minimumCredits,
        string disabledMessage,
        string insufficientCreditsMessage,
        Func<GameEvent, Task<ValidationResult>>? validateAdditionalRequirements = null,
        Func<EFClient, Task>? customJoinAsync = null,
        Func<GameEvent, Task>? handleJoinSuccessAsync = null,
        Func<GameEvent, Task>? handleLeaveSuccessAsync = null)
    {
        // Check if game is enabled
        if (!isGameEnabled)
        {
            gameEvent.Origin.Tell(disabledMessage);
            return;
        }

        // Check if player is already in the game
        if (!_gameManager.IsPlayerPlaying(gameEvent.Origin))
        {
            // Check if player is in any other active game
            if (gameTracker.IsPlayerInAnyGame(gameEvent.Origin, _gameManager))
            {
                var otherGameName = gameTracker.GetGameNamePlayerIsIn(gameEvent.Origin, _gameManager);
                gameEvent.Origin.Tell(credifyConfig.Translations.Core.AlreadyInAnotherGame
                    .FormatExt(otherGameName ?? "another game"));
                return;
            }

            // Validate minimum credits
            var funds = await persistenceService.GetClientCreditsAsync(gameEvent.Origin);
            if (funds < minimumCredits)
            {
                gameEvent.Origin.Tell(insufficientCreditsMessage);
                return;
            }

            // Allow additional validation
            if (validateAdditionalRequirements != null)
            {
                var additionalValidationResult = await validateAdditionalRequirements(gameEvent);
                if (!additionalValidationResult.IsValid)
                {
                    gameEvent.Origin.Tell(additionalValidationResult.ErrorMessage);
                    return;
                }
            }

            // Join the game
            if (customJoinAsync != null)
            {
                await customJoinAsync(gameEvent.Origin);
            }
            else
            {
                await _gameManager.JoinGameAsync(gameEvent.Origin);
            }

            // Send join messages
            if (handleJoinSuccessAsync != null)
            {
                await handleJoinSuccessAsync(gameEvent);
            }
        }
        else
        {
            // Leave the game
            await _gameManager.LeaveGameAsync(gameEvent.Origin);
            if (handleLeaveSuccessAsync != null)
            {
                await handleLeaveSuccessAsync(gameEvent);
            }
        }
    }

    /// <summary>
    /// Result of additional validation.
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; init; }
        public string ErrorMessage { get; init; } = string.Empty;

        public static ValidationResult Valid => new() { IsValid = true };
        public static ValidationResult Invalid(string errorMessage) => new() { IsValid = false, ErrorMessage = errorMessage };
    }
}

using Credify.Chat.Active.Core;
using Credify.Configuration;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Database.Models;

namespace Credify.Commands;

/// <summary>
/// Base class for game join commands that provides common functionality.
/// Implements the template method pattern to eliminate code duplication.
/// </summary>
/// <typeparam name="TManager">The game manager type that implements IActiveGame</typeparam>
public abstract class BaseGameJoinCommand<TManager> : Command where TManager : IActiveGame
{
    protected readonly TManager GameManager;
    protected readonly CredifyConfiguration CredifyConfig;
    protected readonly PersistenceService PersistenceService;

    protected BaseGameJoinCommand(
        CommandConfiguration config,
        ITranslationLookup translationLookup,
        TManager gameManager,
        CredifyConfiguration credifyConfig,
        PersistenceService persistenceService)
        : base(config, translationLookup)
    {
        GameManager = gameManager;
        CredifyConfig = credifyConfig;
        PersistenceService = persistenceService;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        // Check if game is enabled
        if (!IsGameEnabled)
        {
            gameEvent.Origin.Tell(DisabledMessage);
            return;
        }

        // Check if player is already in the game
        if (!GameManager.IsPlayerPlaying(gameEvent.Origin))
        {
            // Validate minimum credits
            var funds = await PersistenceService.GetClientCreditsAsync(gameEvent.Origin);
            if (funds < MinimumCredits)
            {
                gameEvent.Origin.Tell(CredifyConfig.Translations.Core.InsufficientCredits);
                return;
            }

            // Allow derived classes to perform additional validation
            var additionalValidationResult = await ValidateAdditionalJoinRequirementsAsync(gameEvent);
            if (!additionalValidationResult.IsValid)
            {
                gameEvent.Origin.Tell(additionalValidationResult.ErrorMessage);
                return;
            }

            // Join the game
            await JoinGameAsync(gameEvent.Origin);

            // Send join messages
            await HandleJoinSuccessAsync(gameEvent);
        }
        else
        {
            // Leave the game
            await GameManager.LeaveGameAsync(gameEvent.Origin);
            await HandleLeaveSuccessAsync(gameEvent);
        }
    }

    /// <summary>
    /// Gets whether the game is enabled.
    /// </summary>
    protected abstract bool IsGameEnabled { get; }

    /// <summary>
    /// Gets the minimum credits required to join the game.
    /// </summary>
    protected abstract long MinimumCredits { get; }

    /// <summary>
    /// Gets the message to display when the game is disabled.
    /// </summary>
    protected abstract string DisabledMessage { get; }

    /// <summary>
    /// Joins the player to the game. Override for custom join logic.
    /// </summary>
    protected virtual Task JoinGameAsync(EFClient player)
    {
        return GameManager.JoinGameAsync(player);
    }

    /// <summary>
    /// Validates additional join requirements. Override to add custom validation.
    /// </summary>
    protected virtual Task<ValidationResult> ValidateAdditionalJoinRequirementsAsync(GameEvent gameEvent)
    {
        return Task.FromResult(ValidationResult.Valid);
    }

    /// <summary>
    /// Handles successful join. Override to customize join messages.
    /// </summary>
    protected virtual Task HandleJoinSuccessAsync(GameEvent gameEvent)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles successful leave. Override to customize leave messages.
    /// </summary>
    protected virtual Task HandleLeaveSuccessAsync(GameEvent gameEvent)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Result of additional validation.
    /// </summary>
    protected class ValidationResult
    {
        public bool IsValid { get; init; }
        public string ErrorMessage { get; init; } = string.Empty;

        public static ValidationResult Valid => new() { IsValid = true };
        public static ValidationResult Invalid(string errorMessage) => new() { IsValid = false, ErrorMessage = errorMessage };
    }
}

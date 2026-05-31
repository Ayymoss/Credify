using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.Blackjack.Enums;

namespace Credify.Chat.Active.Games.Blackjack;

/// <summary>
/// Tracks the current Blackjack game state. Starts in WaitingForPlayers.
/// </summary>
internal class BlackjackStateMachine() : GameStateMachine<GameState>(GameState.WaitingForPlayers);

using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.Poker.Enums;

namespace Credify.Chat.Active.Games.Poker;

/// <summary>
/// Tracks the current Poker game state. Starts in WaitingForPlayers.
/// </summary>
internal class PokerStateMachine() : GameStateMachine<PokerGameState>(PokerGameState.WaitingForPlayers);

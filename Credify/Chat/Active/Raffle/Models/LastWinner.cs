namespace Credify.Chat.Active.Raffle.Models;

public record LastWinner(int ClientId, string ClientName, long Amount, int PreviousPlayers);

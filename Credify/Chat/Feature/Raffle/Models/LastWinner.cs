namespace Credify.Chat.Feature.Raffle.Models;

public record LastWinner(int ClientId, string ClientName, long Amount, int PreviousPlayers);

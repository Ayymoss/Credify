using Credify.Chat.Active.Raffle.Enums;

namespace Credify.Chat.Active.Raffle.Models;

public record RaffleResult(StatusTypes Status, int? Ticket);

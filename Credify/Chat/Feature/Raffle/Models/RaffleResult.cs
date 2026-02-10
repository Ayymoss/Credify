using Credify.Chat.Feature.Raffle.Enums;

namespace Credify.Chat.Feature.Raffle.Models;

public record RaffleResult(StatusTypes Status, int? Ticket);

using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Raffle.Models;

public record Player(int ClientId, int Ticket);
public record PlayerFull(EFClient Client, int Ticket);



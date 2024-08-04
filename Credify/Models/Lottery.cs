namespace Credify.Models;

public record Lottery(int ClientId, string CleanedName)
{
    public required long Tickets { get; set; }
}

namespace Credify.Models;

public record Lottery(int ClientId)
{
    public string CleanedName { get; set; } = null!;
    public long Tickets { get; set; }
}

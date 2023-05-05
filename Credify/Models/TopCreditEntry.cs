namespace Credify.Models;

public record TopCreditEntry
{
    public int ClientId { get; init; }
    public long Credits { get; set; }
}

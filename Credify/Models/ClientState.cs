namespace Credify.Models;

public record ClientState
{
    public int Score { get; set; }
    public string TeamName { get; set; } = null!;
}

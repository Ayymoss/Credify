namespace Credify.Games.Minefield;

/// <summary>
/// Builds a Minefield playing field: a boolean array where <c>true</c> marks a mine, shuffled with the
/// shared crypto <see cref="Shuffle"/> so placement can't be predicted client-side. Shared by chat and web.
/// </summary>
public static class MinefieldField
{
    public static bool[] Build(int totalTiles, int mines)
    {
        var field = new bool[totalTiles];
        for (var i = 0; i < mines && i < totalTiles; i++)
        {
            field[i] = true;
        }

        Shuffle.InPlace(field);
        return field;
    }
}

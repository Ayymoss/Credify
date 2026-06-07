using Credify.Games.Cards;
using Microsoft.AspNetCore.Components;

namespace Credify.Components.Games.Blackjack;

public partial class CardView
{
    [Parameter, EditorRequired] public Card? Card { get; set; }
    [Parameter] public bool FaceDown { get; set; }
    [Parameter] public bool Reveal { get; set; }
    [Parameter] public int Index { get; set; }
}

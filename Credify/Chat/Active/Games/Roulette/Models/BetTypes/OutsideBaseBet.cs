using Credify.Chat.Active.Games.Roulette.Enums;

namespace Credify.Chat.Active.Games.Roulette.Models.BetTypes;

public abstract class OutsideBaseBet(OutsideBet type, int stake) : BaseBet(stake)
{
    public OutsideBet Type { get; } = type;
}

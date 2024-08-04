using Credify.Chat.Active.Roulette.Enums;

namespace Credify.Chat.Active.Roulette.Models.BetTypes;

public abstract class OutsideBaseBet(OutsideBet type, int stake) : BaseBet(stake)
{
    public OutsideBet Type { get; } = type;
}

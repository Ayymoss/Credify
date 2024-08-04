using Credify.Chat.Active.Roulette.Enums;

namespace Credify.Chat.Active.Roulette.Models.BetTypes;

public abstract class InsideBaseBet(InsideBet type, int stake) : BaseBet(stake)
{
    public InsideBet Type { get; } = type;
}

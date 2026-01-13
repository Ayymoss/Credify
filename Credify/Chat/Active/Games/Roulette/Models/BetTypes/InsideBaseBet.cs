using Credify.Chat.Active.Games.Roulette.Enums;

namespace Credify.Chat.Active.Games.Roulette.Models.BetTypes;

public abstract class InsideBaseBet(InsideBet type, int stake) : BaseBet(stake)
{
    public InsideBet Type { get; } = type;
}

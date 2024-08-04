using Credify.Chat.Active.Roulette.Models.BetTypes;
using SharedLibraryCore;
using SharedLibraryCore.Helpers;

// ReSharper disable AccessToDisposedClosure

namespace Credify.Chat.Active.Blackjack.Rewrite;

public class HandleInput(PersistenceManager persistenceManager)
{
    public record PlayerBet(Player Player, int Bet);

    public async Task<List<PlayerBet>> GetPlayerBetsAsync(List<Player> players, CancellationToken token)
    {
        using var tokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(tokenSource.Token, token);

        var tasks = players.Select(player => Task.Run(async () =>
        {
            var result = await player.Client.PromptClientInput(["[BJ] How much do you want to bet?"], async input =>
            {
                var innerResult = new ParsedInputResult<int>();
                if (!int.TryParse(input, out var bet)) return innerResult.WithError("Invalid input, expected number");
                var credits = await persistenceManager.GetClientCreditsAsync(player.Client);

                if (Math.Abs(bet) > credits) return innerResult.WithError("You don't have enough credits");
                innerResult.Result = Math.Abs(bet);
                return innerResult;
            }, "You took too long to respond", linkedTokenSource.Token);

            return new PlayerBet(player, result.Result);
        }, linkedTokenSource.Token));

        var completedTasks = await Task.WhenAll(tasks);

        return completedTasks.Where(x => x.Bet is not -1).ToList();
    }
}

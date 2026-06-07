using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Credify.Services;
using Microsoft.AspNetCore.Components;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace Credify.Components;

public partial class Credits
{
    [Inject] public required CredifyCache Cache { get; set; }
    [Inject] public required IEntityService<EFClient> ClientService { get; set; }
    [Inject] public required PersistenceService Persistence { get; set; }

    private List<LeaderboardRow> _board = [];
    private long _topBalance;

    protected override async Task OnInitializedAsync()
    {
        // The cached leaderboard only stores ClientId + a (possibly stale) value. Resolve each client's
        // real name and CURRENT balance so the list is accurate, then re-sort by the live balance.
        var rows = new List<LeaderboardRow>();
        foreach (var entry in Cache.TopCredits)
        {
            var client = await ClientService.Get(entry.ClientId);
            if (client is null)
            {
                continue;
            }

            var credits = await Persistence.GetClientCreditsAsync(client);
            rows.Add(new LeaderboardRow(entry.ClientId, client.CleanedName, credits));
        }

        _board = rows.OrderByDescending(r => r.Credits).ToList();
        _topBalance = _board.Count > 0 ? _board[0].Credits : 0;
    }

    // dev tools (e.g. the money-sound tester) only render in DEBUG builds, never in shipped/Release ones.
#if DEBUG
    private static bool ShowDebugTools => true;
#else
    private static bool ShowDebugTools => false;
#endif

    private sealed record LeaderboardRow(int ClientId, string Name, long Credits);
}

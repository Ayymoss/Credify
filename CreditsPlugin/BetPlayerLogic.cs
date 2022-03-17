using System.Diagnostics;
using Data.Abstractions;
using Data.Models.Client.Stats;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;

namespace CreditsPlugin;

public class BetPlayerLogic
{
    public static List<ServerEntry> ServerList;
    
    public BetPlayerLogic(IDatabaseContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    private EFClientStatistics? _pStats = null;
    private readonly IDatabaseContextFactory _contextFactory;
    public static readonly Stopwatch Timer = new();
    public static bool CanBet() => Timer.ElapsedMilliseconds < 120_000;

    // TODO: Fix this entire method
    // https://github.com/RaidMax/IW4M-Admin/blob/master/Plugins/Stats/Commands/ViewStats.cs
    public async void PrimaryLogic(GameEvent e, Server s, int target)
    {
        EFClient playerTarget = null;
        if ((CreditTarget) target == CreditTarget.Origin) playerTarget = e.Origin;
        if ((CreditTarget) target == CreditTarget.Target) playerTarget = e.Target;
        if (playerTarget == null) return;

        var serverId = s.GetIdForServer(e.Owner);

        await using var context = _contextFactory.CreateContext(false);

        if (_pStats == null)
        {
            _pStats = await context.Set<EFClientStatistics>()
                .FirstOrDefaultAsync(c => c.ServerId == serverId.Result && c.ClientId == e.Target.ClientId);
        }
    }
}

public class ServerEntry
{
    public long ServerId { get; set; }
    public int MapTime { get; set; }
}
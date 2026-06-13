using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Credify.Chat.Feature.Achievements;
using Credify.Chat.Feature.Achievements.Models;
using Credify.Chat.Feature.Bounty;
using Credify.Chat.Feature.Raffle;
using Credify.Chat.Feature.Raffle.Models;
using Credify.Chat.Passive.ChatGames;
using Credify.Chat.Passive.Quests;
using Credify.Chat.Passive.Quests.Enums;
using Credify.Chat.Passive.Quests.Models;
using Credify.Configuration;
using Credify.Constants;
using Credify.Games.Live;
using Credify.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace Credify.Components;

public partial class Credits : IDisposable
{
    [Inject] public required CredifyCache Cache { get; set; }
    [Inject] public required IEntityService<EFClient> ClientService { get; set; }
    [Inject] public required PersistenceService Persistence { get; set; }
    [Inject] public required CredifyConfiguration Config { get; set; }
    [Inject] public required IManager Manager { get; set; }
    [Inject] public required ActiveGameTracker Tables { get; set; }
    [Inject] public required CrashLiveRegistry CrashLive { get; set; }
    [Inject] public required RaffleManager Raffle { get; set; }
    [Inject] public required BountyContractManager Bounties { get; set; }
    [Inject] public required QuestManager Quests { get; set; }
    [Inject] public required AchievementManager Achievements { get; set; }
    [Inject] public required PassiveManager Passive { get; set; }

    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    private bool _loading = true;

    // signed-in player strip
    private EFClient? _client;
    private bool _authed;
    private long _balance;
    private string? _myTitle;
    private Dictionary<int, QuestMeta> _myQuests = new(); // keyed by (int)ObjectiveType

    // live floor (refreshed on a timer — all in-memory reads)
    private List<LiveTableInfo> _tables = [];
    private int _crashFlying;
    private List<BountyRow> _bounties = [];

    // raffle (async persistence reads — refreshed every few ticks)
    private int _raffleTickets;
    private LastWinner? _raffleLastWinner;

    // ambient (loaded once on init)
    private List<Quest> _dailyQuests = [];
    private List<Quest> _permanentQuests = [];
    private List<LeaderboardRow> _board = [];
    private List<PurchaseRow> _purchases = [];

    private readonly CancellationTokenSource _cts = new();
    private const int TickSeconds = 5;
    private const int SlowEveryTicks = 6; // raffle/balance re-reads every ~30s

    private int PlayersOnline => Manager.GetActiveClients().Count;
    private int DailiesDone => _dailyQuests.Count(q =>
        _myQuests.TryGetValue((int)q.ObjectiveType, out var meta) && meta.Completed);

    protected override async Task OnInitializedAsync()
    {
        await ResolveSignedInAsync();
        LoadQuests();
        RefreshLiveSnapshots();
        await RefreshRaffleAsync();
        await LoadLeaderboardAsync();
        await LoadPurchasesAsync();
        _loading = false;

        CrashLive.Changed += OnCrashChanged;
        _ = TickLoopAsync();
    }

    // ── signed-in player ─────────────────────────────────────────────────────
    private async Task ResolveSignedInAsync()
    {
        if (AuthState is null)
        {
            return;
        }

        var user = (await AuthState).User;
        if (user.Identity?.IsAuthenticated != true ||
            !int.TryParse(user.FindFirst(ClaimTypes.Sid)?.Value, out var clientId))
        {
            return;
        }

        // prefer the live in-game client (its quest/achievement state is already hydrated and current);
        // fall back to a DB lookup for web-only visitors
        _client = Manager.GetActiveClients().FirstOrDefault(c => c.ClientId == clientId)
                  ?? await ClientService.Get(clientId);
        if (_client is null)
        {
            return;
        }

        _authed = true;
        _balance = await Persistence.GetClientCreditsAsync(_client);

        // hydrate persisted quest/achievement state ONLY if this client object isn't already carrying
        // live state (re-reading over a live in-game client would clobber unsaved progress)
        if (_client.GetAdditionalProperty<List<QuestMeta>>(PluginConstants.ClientQuestsKey) is null)
        {
            await Persistence.ReadClientQuestsAsync(_client);
        }

        if (_client.GetAdditionalProperty<AchievementProgress>(PluginConstants.AchievementsKey) is null)
        {
            await Achievements.LoadAsync(_client);
        }

        _myTitle = Achievements.TopTitle(_client);
        RefreshMyQuests();
    }

    private void RefreshMyQuests()
    {
        if (_client is not null)
        {
            _myQuests = Quests.GetPlayerQuests(_client).ToDictionary(m => m.QuestId);
        }
    }

    // ── per-tick refreshes ───────────────────────────────────────────────────
    private void RefreshLiveSnapshots()
    {
        _tables = Tables.SnapshotTables();
        _crashFlying = CrashLive.Snapshot().Count(e => e.Status == "Flying");
        _bounties = Bounties.GetAllActiveBounties()
            .GroupBy(b => b.TargetClientId)
            .Select(g => new BountyRow(
                g.First().TargetName,
                g.Sum(b => b.Amount),
                g.Count(),
                g.Min(b => b.ExpiresAt)))
            .OrderByDescending(r => r.Total)
            .Take(5)
            .ToList();
    }

    private async Task RefreshRaffleAsync()
    {
        try
        {
            _raffleTickets = (await Raffle.GetPlayersAsync()).Count;
            _raffleLastWinner = await Raffle.GetLastWinnerAsync();
        }
        catch
        {
            // raffle not started yet — leave the panel in its empty state
        }
    }

    private async Task TickLoopAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(TickSeconds));
        var tick = 0;
        try
        {
            while (await timer.WaitForNextTickAsync(_cts.Token))
            {
                tick++;
                RefreshLiveSnapshots();

                if (tick % SlowEveryTicks == 0)
                {
                    await RefreshRaffleAsync();
                    LoadQuests(); // dailies regenerate at midnight
                    if (_client is not null)
                    {
                        _balance = await Persistence.GetClientCreditsAsync(_client);
                        RefreshMyQuests();
                    }
                }

                await InvokeAsync(StateHasChanged);
            }
        }
        catch (OperationCanceledException)
        {
            // page disposed
        }
    }

    private void OnCrashChanged()
    {
        _crashFlying = CrashLive.Snapshot().Count(e => e.Status == "Flying");
        _ = InvokeAsync(StateHasChanged);
    }

    // ── one-shot loads ───────────────────────────────────────────────────────
    private void LoadQuests()
    {
        _dailyQuests = Quests.ActiveQuests.Where(q => !q.IsPermanent).ToList();
        _permanentQuests = Quests.ActiveQuests.Where(q => q.IsPermanent).ToList();
    }

    private async Task LoadLeaderboardAsync()
    {
        // The cached leaderboard only stores ClientId + a (possibly stale) value. Resolve each client's
        // real name, CURRENT balance and top achievement title, then re-sort by the live balance.
        var rows = new List<LeaderboardRow>();
        foreach (var entry in Cache.TopCredits)
        {
            var client = await ClientService.Get(entry.ClientId);
            if (client is null)
            {
                continue;
            }

            var credits = await Persistence.GetClientCreditsAsync(client);
            // a fresh DB instance, so loading persisted achievement state can't clobber live progress
            await Achievements.LoadAsync(client);
            rows.Add(new LeaderboardRow(entry.ClientId, client.CleanedName, credits, Achievements.TopTitle(client)));
        }

        _board = rows.OrderByDescending(r => r.Credits).ToList();
    }

    private async Task LoadPurchasesAsync()
    {
        if (!Config.Shop.IsEnabled)
        {
            return;
        }

        var items = Config.Shop.Items.ToDictionary(i => i.Id, i => i.Name);
        _purchases = (await Persistence.ReadRecentBoughtItemsAsync())
            .OrderByDescending(p => p.Bought)
            .Take(6)
            .Select(p => new PurchaseRow(items.GetValueOrDefault(p.Id, $"Item #{p.Id}"), p.ClientName, p.Bought))
            .ToList();
    }

    // ── display helpers ──────────────────────────────────────────────────────
    private sealed record LeaderboardRow(int ClientId, string Name, long Credits, string? Title);
    private sealed record BountyRow(string Target, long Total, int Contracts, DateTimeOffset? ExpiresAt);
    private sealed record PurchaseRow(string Item, string Buyer, DateTimeOffset At);
    private sealed record TableMetaInfo(string Title, string Route, string Icon);

    private static readonly Dictionary<string, TableMetaInfo> TableMeta = new()
    {
        ["Blackjack"] = new TableMetaInfo("Blackjack", "/credify/blackjack", "ph-cards-three"),
        ["Roulette"] = new TableMetaInfo("Roulette", "/credify/roulette", "ph-circle-half"),
        ["Poker"] = new TableMetaInfo("Texas Hold'em", "/credify/texasholdem", "ph-spade"),
        ["ThreeCardPoker"] = new TableMetaInfo("Three-Card Poker", "/credify/threecardpoker", "ph-cards"),
        ["CasinoHoldem"] = new TableMetaInfo("Casino Hold'em", "/credify/casinoholdem", "ph-club"),
        ["Minefield"] = new TableMetaInfo("Minefield", "/credify/minefield", "ph-bomb"),
    };

    private static TableMetaInfo MetaFor(string name) =>
        TableMeta.GetValueOrDefault(name, new TableMetaInfo(name, "/credify", "ph-game-controller"));

    // Tooltip text for an achievement title chip — the achievement's own description
    // (e.g. "Win 10M total"), so a prestige badge reads sensibly even next to a 0 balance.
    private string? TitleDesc(string? title) =>
        title is null
            ? null
            : Config.Achievement.Achievements.FirstOrDefault(a => a.Name == title)?.Description;

    private (int Progress, bool Done)? MyProgressFor(Quest quest)
    {
        if (!_authed || !_myQuests.TryGetValue((int)quest.ObjectiveType, out var meta))
        {
            return null;
        }

        return (Math.Min(meta.Progress, quest.ObjectiveCount), meta.Completed);
    }

    private static string QuestIcon(ObjectiveType type) => type switch
    {
        ObjectiveType.Kill => "ph-crosshair",
        ObjectiveType.Headshot => "ph-skull",
        ObjectiveType.Melee or ObjectiveType.Humiliation => "ph-knife",
        ObjectiveType.RiotShield => "ph-shield",
        ObjectiveType.Silenced => "ph-speaker-slash",
        ObjectiveType.Impact or ObjectiveType.HotPotato => "ph-bomb",
        ObjectiveType.Suicide => "ph-arrow-fat-line-down",
        ObjectiveType.Chat or ObjectiveType.MyNameJeff => "ph-chat-circle-text",
        ObjectiveType.Trivia => "ph-question",
        ObjectiveType.Raffle => "ph-ticket",
        ObjectiveType.Blackjack => "ph-cards-three",
        ObjectiveType.Roulette => "ph-circle-half",
        ObjectiveType.Minefield => "ph-bomb",
        ObjectiveType.CreditsSpent or ObjectiveType.Baller => "ph-coins",
        ObjectiveType.Donation => "ph-hand-heart",
        ObjectiveType.TopHolder => "ph-crown",
        _ => "ph-target"
    };

    private static string Eta(DateTimeOffset at)
    {
        var span = at - DateTimeOffset.UtcNow;
        if (span <= TimeSpan.Zero)
        {
            return "any moment";
        }

        if (span.TotalDays >= 1)
        {
            return $"{(int)span.TotalDays}d {span.Hours}h";
        }

        return span.TotalHours >= 1 ? $"{(int)span.TotalHours}h {span.Minutes}m" : $"{Math.Max(1, span.Minutes)}m";
    }

    private static string Ago(DateTimeOffset at)
    {
        var span = DateTimeOffset.UtcNow - at;
        if (span.TotalDays >= 1)
        {
            return $"{(int)span.TotalDays}d ago";
        }

        if (span.TotalHours >= 1)
        {
            return $"{(int)span.TotalHours}h ago";
        }

        return span.TotalMinutes >= 1 ? $"{(int)span.TotalMinutes}m ago" : "just now";
    }

    public void Dispose()
    {
        CrashLive.Changed -= OnCrashChanged;
        _cts.Cancel();
        _cts.Dispose();
    }

    // dev tools (e.g. the money-sound tester) only render in DEBUG builds, never in shipped/Release ones.
#if DEBUG
    private static bool ShowDebugTools => true;
#else
    private static bool ShowDebugTools => false;
#endif
}

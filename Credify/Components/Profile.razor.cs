using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Credify.Chat.Feature.Achievements;
using Credify.Chat.Feature.Achievements.Models;
using Credify.Chat.Passive.Quests;
using Credify.Chat.Passive.Quests.Enums;
using Credify.Chat.Passive.Quests.Models;
using Credify.Configuration;
using Credify.Constants;
using Credify.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace Credify.Components;

public partial class Profile
{
    [Inject] public required PersistenceService Persistence { get; set; }
    [Inject] public required AchievementManager Achievements { get; set; }
    [Inject] public required QuestManager Quests { get; set; }
    [Inject] public required CredifyConfiguration Config { get; set; }
    [Inject] public required IManager Manager { get; set; }
    [Inject] public required IEntityService<EFClient> ClientService { get; set; }

    [Parameter] public int? ClientId { get; set; }
    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    private bool _loading = true;
    private bool _notFound;
    private bool _isSelf;
    private int? _viewerId;
    private int _loadedId = -1; // guards against reloading the same profile on re-render

    private EFClient? _target;
    private long _balance;
    private string? _title;
    private AchievementProgress _progress = new();
    private int _questsDone;

    // achievement rows, ordered unlocked-first then by how close the locked ones are to their threshold
    private List<AchRow> _achievements = [];
    private int _unlockedCount;

    // ── lifetime stat readouts (cumulative per-objective totals the achievement system already tracks) ──
    private long Total(ObjectiveType o) => _progress.Totals.GetValueOrDefault((int)o);
    private long Won => Total(ObjectiveType.Baller);
    private long Wagered => Total(ObjectiveType.CreditsSpent);
    private long Kills => Total(ObjectiveType.Kill);
    private long Gifted => Total(ObjectiveType.Donation);

    protected override async Task OnParametersSetAsync()
    {
        // resolve the signed-in viewer once
        if (_viewerId is null && AuthState is not null)
        {
            var user = (await AuthState).User;
            if (user.Identity?.IsAuthenticated == true &&
                int.TryParse(user.FindFirst(ClaimTypes.Sid)?.Value, out var vid))
            {
                _viewerId = vid;
            }
        }

        var targetId = ClientId ?? _viewerId;

        // no target and not signed in → sign-in prompt
        if (targetId is null)
        {
            _loading = false;
            return;
        }

        // already showing this profile (re-render from a child) → nothing to do
        if (targetId == _loadedId)
        {
            return;
        }

        _loadedId = targetId.Value;
        await LoadAsync(targetId.Value);
    }

    private async Task LoadAsync(int targetId)
    {
        _loading = true;
        _notFound = false;
        _isSelf = targetId == _viewerId;

        _target = await ResolveClientAsync(targetId);
        if (_target is null)
        {
            _notFound = true;
            _loading = false;
            return;
        }

        _balance = await Persistence.GetClientCreditsAsync(_target);

        // hydrate persisted state, but never re-read over a live in-game client (would clobber unsaved progress)
        if (_target.GetAdditionalProperty<AchievementProgress>(PluginConstants.AchievementsKey) is null)
        {
            await Achievements.LoadAsync(_target);
        }

        if (_target.GetAdditionalProperty<List<QuestMeta>>(PluginConstants.ClientQuestsKey) is null)
        {
            await Persistence.ReadClientQuestsAsync(_target);
        }

        _progress = Achievements.GetProgress(_target);
        _title = Achievements.TopTitle(_target);
        _questsDone = Quests.GetPlayerQuests(_target).Count(m => m.Completed);

        BuildAchievements();
        _loading = false;
    }

    private void BuildAchievements()
    {
        var all = Config.Achievement.Achievements.Where(a => a.Enabled).ToList();
        _unlockedCount = all.Count(a => _progress.Unlocked.Contains(a.Id));

        _achievements = all
            .Select(a =>
            {
                var have = _progress.Unlocked.Contains(a.Id);
                var current = Math.Min(Total(a.Objective), a.Threshold);
                var pct = a.Threshold > 0 ? (int)Math.Min(100, current * 100 / a.Threshold) : 0;
                return new AchRow(a, have, current, pct);
            })
            // unlocked first (smallest threshold = earliest tier first), then locked by nearest to completion
            .OrderByDescending(r => r.Unlocked)
            .ThenBy(r => r.Unlocked ? r.Achievement.Threshold : long.MaxValue)
            .ThenByDescending(r => r.Pct)
            .ToList();
    }

    private async Task<EFClient?> ResolveClientAsync(int clientId)
    {
        var live = Manager.GetActiveClients().FirstOrDefault(c => c.ClientId == clientId);
        return live ?? await ClientService.Get(clientId);
    }

    // tooltip text for the title chip — the unlocked achievement's own description
    private string? TitleDesc(string? title) =>
        title is null ? null : Config.Achievement.Achievements.FirstOrDefault(a => a.Name == title)?.Description;

    private static string ObjectiveIcon(ObjectiveType o) => o switch
    {
        ObjectiveType.Kill => "ph-crosshair-simple",
        ObjectiveType.Baller => "ph-coins",
        ObjectiveType.CreditsSpent => "ph-hand-coins",
        ObjectiveType.Donation => "ph-gift",
        ObjectiveType.Minefield => "ph-bomb",
        ObjectiveType.Trivia => "ph-brain",
        ObjectiveType.Blackjack => "ph-cards-three",
        ObjectiveType.Roulette => "ph-circle-half",
        ObjectiveType.Headshot => "ph-skull",
        _ => "ph-medal"
    };

    private sealed record AchRow(Achievement Achievement, bool Unlocked, long Current, int Pct);
}

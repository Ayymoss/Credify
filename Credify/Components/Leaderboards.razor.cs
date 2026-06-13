using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Credify.Chat.Passive.Quests.Enums;
using Credify.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace Credify.Components;

public partial class Leaderboards
{
    [Inject] public required LeaderboardService Boards { get; set; }
    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    private const int Limit = 50;

    private int? _viewerId;
    private string _active = "credits";
    private bool _loading = true;
    private List<LeaderboardEntry> _rows = [];

    private sealed record Tab(string Key, string Label, string Icon, string Suffix);

    private static readonly Tab[] TabDefs =
    [
        new("credits", "Credits", "ph-coins", "credits"),
        new("won", "Won", "ph-trend-up", "won"),
        new("wagered", "Wagered", "ph-hand-coins", "wagered"),
        new("kills", "Kills", "ph-crosshair-simple", "kills"),
        new("quests", "Quests", "ph-scroll", "done"),
    ];

    private Tab Active => TabDefs.First(t => t.Key == _active);

    protected override async Task OnInitializedAsync()
    {
        if (AuthState is not null)
        {
            var user = (await AuthState).User;
            if (user.Identity?.IsAuthenticated == true &&
                int.TryParse(user.FindFirst(ClaimTypes.Sid)?.Value, out var id))
            {
                _viewerId = id;
            }
        }

        await LoadAsync();
    }

    private async Task Select(string key)
    {
        if (_active == key)
        {
            return;
        }

        _active = key;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        _rows = [];
        StateHasChanged();

        _rows = _active switch
        {
            "credits" => await Boards.CreditsAsync(Limit),
            "won" => await Boards.ObjectiveAsync(ObjectiveType.Baller, Limit),
            "wagered" => await Boards.ObjectiveAsync(ObjectiveType.CreditsSpent, Limit),
            "kills" => await Boards.ObjectiveAsync(ObjectiveType.Kill, Limit),
            "quests" => await Boards.QuestsAsync(Limit),
            _ => []
        };

        _loading = false;
    }
}

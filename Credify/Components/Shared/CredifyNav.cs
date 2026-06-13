using System;
using System.Collections.Generic;
using System.Linq;
using SharedLibraryCore.Dtos;

namespace Credify.Components.Shared;

/// <summary>
/// Builds the Credify section navigation as a host <see cref="SideContextMenuItems"/> model. Credits sits
/// ungrouped at the top; the games are grouped under casino-style category headers (Table Games / Originals)
/// using the shared SideContextMenu's section-header support — the same mechanism Top Players uses.
/// </summary>
public static class CredifyNav
{
    private static readonly (string Title, string Route, string Icon) Credits =
        ("Credits", "/credify", "ph-fill ph-trophy");

    private static readonly (string Title, string Route, string Icon) ProfileEntry =
        ("Profile", "/credify/profile", "ph-fill ph-user-circle");

    private static readonly (string Title, string Route, string Icon) LeaderboardsEntry =
        ("Leaderboards", "/credify/leaderboards", "ph-fill ph-ranking");

    private static readonly (string Category, (string Title, string Route, string Icon)[] Games)[] Sections =
    [
        ("Table Games",
        [
            ("Blackjack", "/credify/blackjack", "ph-fill ph-cards-three"),
            ("Roulette", "/credify/roulette", "ph-fill ph-circle-half"),
            ("Texas Hold'em", "/credify/texasholdem", "ph-fill ph-spade"),
            ("Casino Hold'em", "/credify/casinoholdem", "ph-fill ph-club"),
            ("Three-Card Poker", "/credify/threecardpoker", "ph-fill ph-cards"),
            ("Video Poker", "/credify/videopoker", "ph-fill ph-spade"),
            ("Baccarat", "/credify/baccarat", "ph-fill ph-cards"),
        ]),
        ("Originals",
        [
            ("Slots", "/credify/slots", "ph-fill ph-cherries"),
            ("Wheel", "/credify/wheel", "ph-fill ph-circle-half"),
            ("Keno", "/credify/keno", "ph-fill ph-grid-four"),
            ("Plinko", "/credify/plinko", "ph-fill ph-circles-three"),
            ("Crash", "/credify/crash", "ph-fill ph-rocket-launch"),
            ("Minefield", "/credify/minefield", "ph-fill ph-bomb"),
        ]),
    ];

    /// <summary>Build the nav model, flagging <paramref name="activeRoute"/> as the current section.</summary>
    public static SideContextMenuItems Build(string activeRoute)
    {
        // Credits is an ungrouped top entry; the games are collapsible category groups (IsCollapse + Meta),
        // the mechanism the shared SideContextMenu already supports (same as Top Players' server groups).
        var items = new List<SideContextMenuItem>
        {
            Link(Credits, activeRoute, category: null),
            Link(ProfileEntry, activeRoute, category: null),
            Link(LeaderboardsEntry, activeRoute, category: null),
        };

        foreach (var (category, games) in Sections)
        {
            items.AddRange(games.Select(game => Link(game, activeRoute, category)));
        }

        return new SideContextMenuItems { MenuTitle = "Credify", Items = items };
    }

    private static SideContextMenuItem Link((string Title, string Route, string Icon) game, string activeRoute, string? category) => new()
    {
        IsLink = true,
        IsCollapse = category is not null,
        Meta = category,
        Title = game.Title,
        Reference = game.Route,
        Icon = game.Icon,
        IsActive = string.Equals(game.Route, activeRoute, StringComparison.OrdinalIgnoreCase),
    };
}

using System;
using System.Collections.Generic;
using System.Linq;
using SharedLibraryCore.Dtos;

namespace Credify.Components.Shared;

/// <summary>
/// Builds the Credify section navigation as a host <see cref="SideContextMenuItems"/> model, so every
/// Credify page renders the standard IW4MAdmin right-aligned nav pane (via WebCommon's PluginPageShell)
/// instead of a bespoke sidebar.
/// </summary>
public static class CredifyNav
{
    private static readonly (string Title, string Route, string Icon)[] Links =
    [
        ("Credits", "/credify", "ph-fill ph-trophy"),
        ("Blackjack", "/credify/blackjack", "ph-fill ph-cards-three"),
        ("Minefield", "/credify/minefield", "ph-fill ph-bomb"),
        ("Roulette", "/credify/roulette", "ph-fill ph-circle-half"),
        ("Crash", "/credify/crash", "ph-fill ph-rocket-launch"),
        ("Poker", "/credify/poker", "ph-fill ph-spade"),
    ];

    /// <summary>Build the nav model, flagging <paramref name="activeRoute"/> as the current section.</summary>
    public static SideContextMenuItems Build(string activeRoute) => new()
    {
        MenuTitle = "Credify",
        Items = Links.Select(link => new SideContextMenuItem
        {
            IsLink = true,
            Title = link.Title,
            Reference = link.Route,
            Icon = link.Icon,
            IsActive = string.Equals(link.Route, activeRoute, StringComparison.OrdinalIgnoreCase),
        }).ToList(),
    };
}

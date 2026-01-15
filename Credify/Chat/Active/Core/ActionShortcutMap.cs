namespace Credify.Chat.Active.Core;

/// <summary>
/// Reusable action shortcut mapping for chat input parsing.
/// Maps short aliases (h, s, f) to full action names or enum values.
/// Supports multiple actions for the same shortcut (e.g., 'c' for Check or Call).
/// </summary>
/// <typeparam name="TAction">The action type (enum or string)</typeparam>
public class ActionShortcutMap<TAction> where TAction : notnull
{
    private readonly Dictionary<string, List<TAction>> _shortcuts = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Adds a shortcut mapping.
    /// </summary>
    public ActionShortcutMap<TAction> Add(string shortcut, TAction action)
    {
        var key = shortcut.ToLower();
        if (!_shortcuts.ContainsKey(key))
        {
            _shortcuts[key] = [];
        }
        if (!_shortcuts[key].Contains(action))
        {
            _shortcuts[key].Add(action);
        }
        return this;
    }

    /// <summary>
    /// Adds multiple shortcuts that map to the same action.
    /// </summary>
    public ActionShortcutMap<TAction> Add(TAction action, params string[] shortcuts)
    {
        foreach (var shortcut in shortcuts)
        {
            Add(shortcut, action);
        }
        return this;
    }

    /// <summary>
    /// Tries to resolve an action from input. Returns the first match.
    /// </summary>
    public bool TryGetAction(string input, out TAction? action)
    {
        if (_shortcuts.TryGetValue(input.Trim().ToLower(), out var actions) && actions.Count > 0)
        {
            action = actions[0];
            return true;
        }
        action = default;
        return false;
    }

    /// <summary>
    /// Gets all potential actions for an input (for ambiguous shortcuts like 'c' for Check/Call).
    /// </summary>
    public List<TAction> GetAllActions(string input)
    {
        return _shortcuts.TryGetValue(input.Trim().ToLower(), out var actions) ? actions : [];
    }

    /// <summary>
    /// Gets action or null if not found.
    /// </summary>
    public TAction? GetActionOrDefault(string input)
    {
        return TryGetAction(input, out var action) ? action : default;
    }
}

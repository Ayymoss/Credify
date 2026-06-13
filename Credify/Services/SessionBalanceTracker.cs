using System;
using System.Collections.Generic;

namespace Credify.Services;

/// <summary>One sample on the session profit/loss curve: the player's cumulative net at that moment.</summary>
public readonly record struct SessionPoint(DateTimeOffset At, long Net);

/// <summary>
/// CIRCUIT-SCOPED running profit/loss curve for the player's current browser session, drawn by the
/// SessionGraphPanel docked under the side nav. Fed by <see cref="GameHistoryService"/> events, so no game
/// page has to report anything. Scoped (not singleton) is the whole point: the Blazor circuit survives
/// enhanced navigation between game pages — the curve follows the player from Plinko to Crash — but a
/// refresh or a return visit gets a fresh circuit and therefore a fresh graph. Nothing is persisted.
/// </summary>
public sealed class SessionBalanceTracker : IDisposable
{
    // beyond this the interior is decimated (every 2nd point dropped) — the curve keeps its shape, the
    // payload to the JS renderer stays bounded even for a marathon turbo-auto session
    private const int MaxPoints = 400;

    private readonly GameHistoryService _history;
    private readonly object _lock = new();
    private readonly List<SessionPoint> _points = [];
    private int? _clientId;
    private DateTimeOffset _startedAt;
    private int _plays;

    /// <summary>Raised after a new sample lands (from any game/thread) so the panel can redraw live.</summary>
    public event Action? Changed;

    public SessionBalanceTracker(GameHistoryService history)
    {
        _history = history;
        _history.Changed += OnRecorded;
    }

    /// <summary>Bind the session to a client and start the curve at net 0. First caller wins; later calls
    /// (each game page renders the panel) are no-ops, which is what keeps one curve across pages.</summary>
    public void Track(int clientId)
    {
        lock (_lock)
        {
            if (_clientId is not null)
            {
                return;
            }

            _clientId = clientId;
            _startedAt = DateTimeOffset.UtcNow;
            _points.Add(new SessionPoint(_startedAt, 0));
        }
    }

    public (IReadOnlyList<SessionPoint> Points, DateTimeOffset StartedAt, int Plays) Snapshot()
    {
        lock (_lock)
        {
            return (_points.ToArray(), _startedAt, _plays);
        }
    }

    private void OnRecorded(int clientId, GameHistoryEntry entry)
    {
        lock (_lock)
        {
            if (_clientId != clientId)
            {
                return;
            }

            _plays++;
            _points.Add(new SessionPoint(entry.At, _points[^1].Net + entry.Net));

            if (_points.Count > MaxPoints)
            {
                // halve by dropping every 2nd interior point; first and latest always survive
                for (var i = _points.Count - 2; i > 0; i -= 2)
                {
                    _points.RemoveAt(i);
                }
            }
        }

        Changed?.Invoke();
    }

    public void Dispose() => _history.Changed -= OnRecorded;
}

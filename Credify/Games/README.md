# Credify games core

Shared, I/O-free game logic. The rules, math and primitives live here **once**; each frontend owns only its
own input/output. This is the pattern to follow when adding a new game type.

```
Games/                         ← shared core (no chat, no Razor, no credits/persistence)
  Shuffle.cs                   crypto Fisher–Yates, used by everything that shuffles
  Cards/
    Card.cs                    one rich card model (distinct J/Q/K, glyph + letter suits, blackjack value)
    CardDeck.cs                52-card composition + draw/reshuffle
  Blackjack/
    GameOutcome.cs             Win / Lose / Push / Blackjack
    BlackjackRules.cs          hand value, bust, natural, dealer-stand, outcome — pure rules
    BlackjackPayouts.cs        outcome + config → credits
  Minefield/
    MinefieldPayoutCalculator  multiplier / payout / odds (standard "Mines" math)
    MinefieldField.cs          shuffled mine field
  Plinko/                      (web-only — no chat frontend, like Crash)
    PlinkoBoard.cs             crypto 50/50 bounce per peg row -> landing bucket
    PlinkoPayoutCalculator     multipliers DERIVED from the binomial pmf + a risk volatility, so the return
                               is exactly the configured house edge for any board (no hand-authored tables)

Chat/Active/Games/<Game>/      ← chat frontend: turn-based text I/O (COD console limits: <5 lines, letters
                                 not glyphs, no interactive input). Consumes the core; adds chat formatting.
Components/Games/<Game>/       ← web frontend: Razor pages, animations, rich rendering. Consumes the SAME core.
```

## Rules of thumb

- **Core has no I/O.** No `IManager`, no `PersistenceService`, no `TellPlayerAsync`, no Razor. If it touches
  credits, the server, chat output or the DOM, it belongs in a frontend, not here.
- **Each frontend formats its own presentation.** Chat renders a `Card` as `"K-H"` (letters — the game
  console can't draw `♥`); the webfront renders the same `Card` with a glyph component. Same data, different
  I/O. See `Chat/.../Blackjack/BlackjackChatFormat.cs` vs `Components/Games/Blackjack/CardView.razor`.
- **One implementation of shared mechanics.** Shuffling, deck composition, hand evaluation and payout math
  are written once and reused. Don't reimplement them per frontend.
- **State machines may differ.** Chat Blackjack is multiplayer with splits/insurance over chat turns; the web
  table is single-player. That's fine — the *frontend flow* differs, the *rules* (BlackjackRules/Payouts) are
  shared. Don't force the state machines together.

## Adding a game

1. Put the rules/math/models under `Games/<Game>/` with no I/O dependencies.
2. Build the chat frontend under `Chat/Active/Games/<Game>/` (turn-based text) consuming the core.
3. Build the web frontend under `Components/Games/<Game>/` (Razor) consuming the same core.

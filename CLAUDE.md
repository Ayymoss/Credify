# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build

```bash
dotnet build Credify.sln
```

Target framework is .NET 10.0. The main plugin project is `Credify/Credify.csproj`. There are also two console apps under `PokerTestHarness/` (GameServer and PlayerClient) used for interactive Poker testing — run each with `dotnet run` in separate terminals.

There is no unit test project or linter configuration.

## What This Is

Credify is an IW4MAdmin plugin that adds a credit economy to Call of Duty servers. Players earn credits from kills, win them in games (Blackjack, Poker, Roulette, slots, coin flip, RPS), compete in timed chat games (trivia, math, typing tests), complete quests, buy shop items, enter raffles, and place bounties.

IW4MAdmin is the host application. It provides the plugin interface (`IPluginV2`), event system, persistence (`IMetaServiceV2`), command framework, server management (`IManager`), and player model (`EFClient`).

## Architecture

### Plugin Entry Point — `Plugin.cs`

Implements `IPluginV2`. `RegisterDependencies()` wires all services as singletons. The constructor subscribes to IW4MAdmin events (ClientKilled, ClientMessaged, ClientStateAuthorized, ClientStateDisposed, Load). `OnLoad()` initializes game managers and the scheduler.

### Two Game Categories

**Active Games** (`Chat/Active/`) — Continuous, looping games (Blackjack, Poker, Roulette). Inheritance: `BaseActiveGame<TPlayer>` → `BaseContinuousGame<TPlayer>` → concrete game. Each game has a Manager class that wraps the game with I/O handlers. `BaseContinuousGame` provides the game loop with `ManualResetEventSlim` signaling when players join.

**Passive Chat Games** (`Chat/Passive/ChatGames/`) — Auto-running timed games (Trivia, Countdown, MathTest, TypingTest, Acronym, CompleteTheWord). All extend `ChatGame` abstract base. `PassiveManager` picks a random game type on a schedule and manages its lifecycle. The `ChatGame.CalculateReactionTime()` method handles fair cross-server timing by subtracting each server's log pipeline latency (via IW4MAdmin's `LatencyMetrics`) from the raw wall-clock reaction time.

### Persistence — Facade Pattern

`PersistenceService` is a facade over specialized services: `CreditsService`, `StatisticsService`, `BankService`, `ShopPersistenceService`, `QuestPersistenceService`, `RafflePersistenceService`. All data is stored via IW4MAdmin's `IMetaServiceV2` using string keys defined in `PluginConstants`. `CreditsService` uses per-client `SemaphoreSlim` locks to prevent race conditions on balance updates.

### Event Flow

IW4MAdmin events → `EventHandlers/` classes → route to managers/services. `ClientMessagedEventHandler` fans out chat messages to `PassiveManager`, `BlackjackManager`, `QuestManager`, and `PokerManager` in parallel. `ClientKilledEventHandler` handles credit rewards, streak tracking, and bounty claims.

A custom static pub/sub system (`ICredifyEventService`) raises `ObjectiveType` events (Kill, Headshot, CreditsSpent, etc.) that drive quest progress tracking.

### Latency Compensation — `ChatGame.CalculateReactionTime()`

Passive chat games use IW4MAdmin's `server.LatencyMetrics` (available via `client.CurrentServer.LatencyMetrics`) to compensate for RCON/log ingestion lag. The raw wall-clock reaction time (`answerEventTime - broadcastTime`) is adjusted by subtracting `GameLogPipelineMs` (precise, requires GSC companion) or `RconRoundTripMs / 2` (estimated one-way, fallback). This makes reaction times comparable across servers with different latencies.

### Configuration — `Configuration/CredifyConfiguration.cs`

Root config object containing sub-configs for each feature. Stored under metadata key `"CredifyConfigurationV3"`. Translations are nested under `TranslationsRoot` with per-feature translation classes using `{{placeholder}}` syntax and IW4MAdmin color codes like `(Color::Accent)`.

### Commands — `Commands/`

Each command extends `SharedLibraryCore.Commands.Command`. `CommandDiscoveryService` uses reflection to find all commands and build the categorized help menu. Commands use `[CommandCategory("...")]` for grouping.

## Key Patterns

- All services registered as singletons — they hold state across the plugin lifetime
- `ConcurrentDictionary` for thread-safe player tracking in games
- `SemaphoreSlim` for async-safe per-client credit locking
- `Utilities.ExecuteAfterDelay()` for scheduling (timeouts, grace periods, game rounds)
- `CredifyCache` provides in-memory cache for leaderboards and statistics
- `FormatExt()` extension method on strings for template placeholder replacement
- Client data stored via `GetAdditionalProperty<T>(key)` / `SetAdditionalProperty(key, value)` for in-memory, `IMetaServiceV2` for persistent

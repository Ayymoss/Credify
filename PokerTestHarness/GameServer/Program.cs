using System.IO.Pipes;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using GameServer.Mocks;
using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.Poker;
using Credify.Chat.Active.Games.Poker.Models;
using Credify.Chat.Active.Games.Poker.Services;
using Credify.Chat.Active.Games.Poker.Utilities;
using Credify.Configuration;
using Credify.Configuration.Translations;
using Credify.Services;
using Data.Models;
using SharedLibraryCore.Database.Models;
using Spectre.Console;

namespace GameServer;

class Program
{
    private static readonly ConcurrentDictionary<string, EFClient> _players = new();
    private static readonly ConcurrentDictionary<string, StreamWriter> _playerPipes = new();
    private static PokerTable? _pokerTable;
    private static readonly object _consoleLock = new();
    private const long InitialCredits = 10000;

    // Map IW4MAdmin color codes to Spectre.Console colors
    private static readonly Dictionary<string, string> ColorMap = new()
    {
        { "Pink", "deeppink1" },
        { "White", "white" },
        { "Yellow", "yellow" },
        { "Green", "green" },
        { "Red", "red" },
        { "Accent", "cyan1" },
        { "Blue", "blue" },
        { "Gray", "grey" },
        { "Orange", "orange1" }
    };

    static async Task Main(string[] args)
    {
        Console.Title = "Poker Test Harness - Game Server";
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔══════════════════════════════════════════════╗");
        Console.WriteLine("║       POKER TEST HARNESS - GAME SERVER       ║");
        Console.WriteLine("╚══════════════════════════════════════════════╝");
        Console.ResetColor();
        Console.WriteLine();

        // Create mock infrastructure - all services will use in-memory storage
        var metaService = new MockMetaService();
        var cache = new CredifyCache();
        var statisticsService = new StatisticsService(metaService, cache);
        var creditsService = new CreditsService(metaService, statisticsService);
        var bankService = new BankService(metaService, cache);
        var shopPersistenceService = new ShopPersistenceService(metaService);
        var questPersistenceService = new QuestPersistenceService(metaService);
        var rafflePersistenceService = new RafflePersistenceService(metaService);

        var persistenceService = new PersistenceService(
            creditsService,
            statisticsService,
            bankService,
            shopPersistenceService,
            questPersistenceService,
            rafflePersistenceService);

        // Create configuration with reasonable timeouts for testing
        var config = new CredifyConfiguration
        {
            Poker = new PokerConfiguration
            {
                MinPlayers = 2,
                SmallBlind = 50,
                BigBlind = 100,
                TimeoutForPlayerAction = TimeSpan.FromSeconds(60),
                MinimumBuyIn = 1000,
                MaximumBuyIn = 100000
            }
        };
        var translations = new TranslationsRoot();

        // Create poker services
        var deckService = new PokerDeckService();
        var handEvaluator = new PokerHandEvaluator();
        var bettingService = new PokerBettingService(config.Poker.SmallBlind, config.Poker.BigBlind);
        var actionValidator = new PokerActionValidator(bettingService);
        var handleInput = new PokerHandleInput(actionValidator, translations.Poker);

        // Create mock output handler that bypasses EFClient.Tell() entirely
        var handleOutput = new MockPokerHandleOutput(translations, OnPlayerMessage);
        
        // Communication is still needed for PokerTable constructor but won't be used for output
        // (PokerTable uses the IGameOutputHandler for actual output)
        var communication = new GamePlayerCommunication();

        // Create the REAL poker table with mock output
        _pokerTable = new PokerTable(
            config,
            translations,
            persistenceService,
            communication,
            handleInput,
            handleInput,
            handleOutput,
            deckService,
            handEvaluator,
            bettingService,
            actionValidator);

        // Start game loop in background
        var cts = new CancellationTokenSource();
        var gameLoopTask = Task.Run(async () =>
        {
            try
            {
                await _pokerTable.GameLoopAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Normal exit
            }
            catch (Exception ex)
            {
                LogServer($"Game loop error: {ex.Message}");
            }
        });

        // Start pipe servers for players
        var player1Task = StartPlayerPipeServer("Player1", 1, persistenceService, cts.Token);
        var player2Task = StartPlayerPipeServer("Player2", 2, persistenceService, cts.Token);

        LogServer("Waiting for players to connect...");
        LogServer("Run: dotnet run --project PlayerClient -- --name Player1");
        LogServer("Run: dotnet run --project PlayerClient -- --name Player2");
        Console.WriteLine();

        // Wait for exit command
        Console.WriteLine("Press 'Q' to quit");
        while (true)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Q)
                {
                    break;
                }
            }
            await Task.Delay(100);
        }

        cts.Cancel();
        await Task.WhenAll(gameLoopTask, player1Task, player2Task);
    }

    private static async Task StartPlayerPipeServer(string playerName, int clientId, PersistenceService persistenceService, CancellationToken token)
    {
        // Create EFClient ONCE per player slot, reuse across reconnections
        // NetworkId is used for GetHashCode/Equals in EFClient, so we must set it
        var client = new EFClient
        {
            ClientId = clientId,
            NetworkId = clientId, // Required for proper hash code
            CurrentAlias = new EFAlias { Name = playerName }
        };
        // Name comes from CurrentAlias.Name, CleanedName strips colors from Name
        
        // Give player initial credits once
        await persistenceService.AddCreditsAsync(client, InitialCredits);
        _players[playerName] = client;
        
        while (!token.IsCancellationRequested)
        {
            try
            {
                var pipeName = $"PokerPlayer{playerName}";
                await using var pipeServer = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                LogServer($"Waiting for {playerName} to connect on pipe: {pipeName}");
                await pipeServer.WaitForConnectionAsync(token);
                LogServer($"{playerName} connected!");

                // Set up reader/writer
                var reader = new StreamReader(pipeServer);
                var writer = new StreamWriter(pipeServer) { AutoFlush = true };
                _playerPipes[playerName] = writer;

                // Send welcome message
                var credits = await persistenceService.GetClientCreditsAsync(client);
                await writer.WriteLineAsync($"Connected to Poker server as {playerName}");
                await writer.WriteLineAsync($"Current credits: {credits:N0}");
                await writer.WriteLineAsync("Commands: /join, /leave, /quit, cards, river, c/f/r X/a");
                await writer.WriteLineAsync("");

                // Handle messages
                while (pipeServer.IsConnected && !token.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync(token);
                    if (line == null) break;

                    await HandlePlayerInput(playerName, client, line, writer);
                }

                _playerPipes.TryRemove(playerName, out _);
                
                // CRITICAL: Notify the game that player left!
                if (_pokerTable!.IsPlayerPlaying(client))
                {
                    LogServer($"{playerName} leaving game due to disconnect...");
                    await _pokerTable.LeaveGameAsync(client);
                }
                
                LogServer($"{playerName} disconnected");
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                LogServer($"Pipe error for {playerName}: {ex.Message}");
                LogServer($"Stack trace: {ex.StackTrace}");
                await Task.Delay(1000, token);
            }
        }
    }

    private static async Task HandlePlayerInput(string playerName, EFClient client, string input, StreamWriter writer)
    {
        var trimmed = input.Trim().ToLower();
        LogServer($"[{playerName}] < {input}");

        switch (trimmed)
        {
            case "/join":
                // Debug: log client identity
                var isPlaying = _pokerTable!.IsPlayerPlaying(client);
                LogServer($"[DEBUG] Client {client.ClientId} ({client.Name}), HashCode: {client.GetHashCode()}, IsPlaying: {isPlaying}");
                
                if (isPlaying)
                {
                    await writer.WriteLineAsync("Already in the game!");
                }
                else
                {
                    await _pokerTable.JoinGameAsync(client);
                    await writer.WriteLineAsync("Joining poker game...");
                }
                break;

            case "/leave":
                if (_pokerTable!.IsPlayerPlaying(client))
                {
                    await _pokerTable.LeaveGameAsync(client);
                    await writer.WriteLineAsync("Left the poker game");
                }
                else
                {
                    await writer.WriteLineAsync("Not in a game!");
                }
                break;

            case "/quit":
                await writer.WriteLineAsync("Goodbye!");
                break;

            case "cards":
                // Show player's hole cards and community cards
                if (_pokerTable!.IsPlayerPlaying(client))
                {
                    await _pokerTable.ShowCardsAsync(client, showRiverOnly: false);
                }
                else
                {
                    await writer.WriteLineAsync("Not in a game!");
                }
                break;

            case "river":
                // Show only community cards
                if (_pokerTable!.IsPlayerPlaying(client))
                {
                    await _pokerTable.ShowCardsAsync(client, showRiverOnly: true);
                }
                else
                {
                    await writer.WriteLineAsync("Not in a game!");
                }
                break;

            default:
                // Route to poker game
                if (_pokerTable!.IsPlayerPlaying(client))
                {
                    await _pokerTable.HandleChatAsync(client, input);
                }
                else
                {
                    await writer.WriteLineAsync("Type /join to join the game first");
                }
                break;
        }
    }

    private static void OnPlayerMessage(EFClient player, IEnumerable<string> messages)
    {
        // Name comes from CurrentAlias.Name
        var playerName = player.Name ?? "Unknown";
        var messageList = messages.ToList();

        // Log to server console WITH colors via Spectre
        foreach (var msg in messageList)
        {
            LogServerColored(playerName, msg);
        }

        // Send to player's pipe WITH color codes for client to render
        if (_playerPipes.TryGetValue(playerName, out var writer))
        {
            foreach (var msg in messageList)
            {
                try
                {
                    writer.WriteLine(msg); // Keep color codes!
                }
                catch
                {
                    // Pipe may be broken
                }
            }
        }
    }

    private static void LogServer(string message)
    {
        lock (_consoleLock)
        {
            var timestamp = $"[grey][[{DateTime.Now:HH:mm:ss}]][/] ";
            try
            {
                // Convert colors and output with Spectre
                var coloredMessage = ConvertToSpectreMarkup(message);
                AnsiConsole.MarkupLine(timestamp + coloredMessage);
            }
            catch
            {
                // Fallback if markup fails
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {StripColorCodes(message)}");
            }
        }
    }

    private static void LogServerColored(string playerName, string message)
    {
        lock (_consoleLock)
        {
            var timestamp = $"[grey][[{DateTime.Now:HH:mm:ss}]][/] ";
            var prefix = $"[grey][[To {Markup.Escape(playerName)}]][/] ";
            try
            {
                var coloredMessage = ConvertToSpectreMarkup(message);
                AnsiConsole.MarkupLine(timestamp + prefix + coloredMessage);
            }
            catch
            {
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [To {playerName}] {StripColorCodes(message)}");
            }
        }
    }

    private static string StripColorCodes(string text)
    {
        return Regex.Replace(text, @"\(Color::[^)]+\)", "");
    }

    private static string ConvertToSpectreMarkup(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // Escape square brackets first
        text = Markup.Escape(text);

        // Pattern matches (Color::Name) - after Escape they become escaped
        // We need to unescape our markers first
        text = text.Replace("(Color::", "(Color::");

        var result = new System.Text.StringBuilder();
        var lastIndex = 0;
        var matches = Regex.Matches(text, @"\(Color::(\w+)\)");
        string? currentColor = null;

        foreach (Match match in matches)
        {
            // Add text before this color code
            if (match.Index > lastIndex)
            {
                result.Append(text.Substring(lastIndex, match.Index - lastIndex));
            }

            // Close previous color if any
            if (currentColor != null)
            {
                result.Append("[/]");
            }

            // Get new color
            var colorName = match.Groups[1].Value;
            if (ColorMap.TryGetValue(colorName, out var spectreColor))
            {
                result.Append($"[{spectreColor}]");
                currentColor = spectreColor;
            }
            else
            {
                result.Append("[white]");
                currentColor = "white";
            }

            lastIndex = match.Index + match.Length;
        }

        // Add remaining text
        if (lastIndex < text.Length)
        {
            result.Append(text.Substring(lastIndex));
        }

        // Close final color if any
        if (currentColor != null)
        {
            result.Append("[/]");
        }

        return result.ToString();
    }
}

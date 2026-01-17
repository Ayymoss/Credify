using System.IO.Pipes;
using System.Text.RegularExpressions;
using Spectre.Console;

namespace PlayerClient;

class Program
{
    // Map IW4MAdmin color codes to Spectre.Console colors
    private static readonly Dictionary<string, string> ColorMap = new()
    {
        { "Pink", "deeppink1" },
        { "White", "white" },
        { "Yellow", "yellow" },
        { "Green", "green" },
        { "Red", "red" },
        { "Accent", "cyan1" },  // Accent = a nice highlight color
        { "Blue", "blue" },
        { "Gray", "grey" },
        { "Orange", "orange1" }
    };

    static async Task Main(string[] args)
    {
        var playerName = "Player1";
        var gameType = "Poker";
        
        // Parse arguments
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--name" || args[i] == "-n")
            {
                playerName = args[i + 1];
            }
            else if (args[i] == "--game" || args[i] == "-g")
            {
                gameType = args[i + 1];
            }
        }

        Console.Title = $"{gameType} Player - {playerName}";
        
        // Print header using Spectre
        AnsiConsole.Write(new Rule($"[green]{gameType.ToUpper()} PLAYER: {playerName}[/]").RuleStyle("green"));
        AnsiConsole.WriteLine();

        var pipeName = $"{gameType}Player{playerName}";
        
        try
        {
            AnsiConsole.MarkupLine($"[grey]Connecting to server on pipe: {pipeName}...[/]");
            
            await using var pipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipeClient.ConnectAsync(10000); // 10 second timeout
            
            AnsiConsole.MarkupLine("[green]Connected![/]");
            AnsiConsole.WriteLine();

            var reader = new StreamReader(pipeClient);
            var writer = new StreamWriter(pipeClient) { AutoFlush = true };

            // Start read task
            var cts = new CancellationTokenSource();
            var readTask = Task.Run(async () =>
            {
                try
                {
                    while (!cts.Token.IsCancellationRequested && pipeClient.IsConnected)
                    {
                        var line = await reader.ReadLineAsync(cts.Token);
                        if (line == null) break;
                        
                        PrintColoredMessage(line);
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    AnsiConsole.MarkupLine($"[red]Read error: {ex.Message}[/]");
                }
            });

            // Main input loop - commands will be shown from server welcome message
            AnsiConsole.WriteLine();

            while (pipeClient.IsConnected)
            {
                AnsiConsole.Markup($"[cyan]{playerName} > [/]");
                var input = Console.ReadLine();
                if (string.IsNullOrEmpty(input)) continue;

                if (input.Trim().ToLower() == "/quit")
                {
                    break;
                }

                try
                {
                    await writer.WriteLineAsync(input);
                }
                catch
                {
                    AnsiConsole.MarkupLine("[red]Disconnected from server.[/]");
                    break;
                }
            }

            cts.Cancel();
            await readTask;
        }
        catch (TimeoutException)
        {
            AnsiConsole.MarkupLine("[red]Could not connect to server. Make sure GameServer is running.[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {Markup.Escape(ex.Message)}[/]");
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey]Press any key to exit...[/]");
        Console.ReadKey();
    }

    /// <summary>
    /// Converts IW4MAdmin (Color::X) format to Spectre.Console markup and prints it.
    /// </summary>
    private static void PrintColoredMessage(string message)
    {
        // Add timestamp
        var timestamp = $"[grey][[{DateTime.Now:HH:mm:ss}]][/] ";

        // Convert (Color::X) format to Spectre markup [color]
        // Pattern: (Color::Name) where Name is the color
        var converted = ConvertToSpectreMarkup(message);
        
        try
        {
            AnsiConsole.MarkupLine(timestamp + converted);
        }
        catch
        {
            // If markup fails (e.g., unescaped brackets), print plain
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");
        }
    }

    /// <summary>
    /// Converts IW4MAdmin color codes to Spectre.Console markup format.
    /// Example: "(Color::Pink)Hello" becomes "[deeppink1]Hello[/]"
    /// </summary>
    private static string ConvertToSpectreMarkup(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // Escape any existing square brackets in the text first
        text = text.Replace("[", "[[").Replace("]", "]]");

        // Pattern to match (Color::Name) 
        var pattern = @"\(\(Color::(\w+)\)\)"; // After escaping [ and ], pattern becomes ((Color::X))
        
        // Track which color is currently "open"
        var result = text;
        var colorStack = new Stack<string>();
        
        // Simpler approach: just replace each (Color::X) with opening/closing tags
        // Since colors change inline, we'll close the previous and open the new
        
        // First, unescape our color codes (they won't have brackets inside)
        result = Regex.Replace(result, @"\(\(Color::(\w+)\)\)", "(Color::$1)");
        
        // Now process the colors
        var outputParts = new List<string>();
        var lastIndex = 0;
        var matches = Regex.Matches(result, @"\(Color::(\w+)\)");
        
        string? currentColor = null;
        
        foreach (Match match in matches)
        {
            // Add text before this color code
            if (match.Index > lastIndex)
            {
                var textBefore = result.Substring(lastIndex, match.Index - lastIndex);
                outputParts.Add(textBefore);
            }
            
            // Close previous color if any
            if (currentColor != null)
            {
                outputParts.Add("[/]");
            }
            
            // Get new color
            var colorName = match.Groups[1].Value;
            if (ColorMap.TryGetValue(colorName, out var spectreColor))
            {
                outputParts.Add($"[{spectreColor}]");
                currentColor = spectreColor;
            }
            else
            {
                // Unknown color, use white
                outputParts.Add("[white]");
                currentColor = "white";
            }
            
            lastIndex = match.Index + match.Length;
        }
        
        // Add remaining text
        if (lastIndex < result.Length)
        {
            outputParts.Add(result.Substring(lastIndex));
        }
        
        // Close final color if any
        if (currentColor != null)
        {
            outputParts.Add("[/]");
        }
        
        return string.Join("", outputParts);
    }
}

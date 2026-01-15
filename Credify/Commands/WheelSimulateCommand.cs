using Credify.Commands.Attributes;
using Credify.Configuration;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;
using System.Collections.Concurrent;
using Credify.Services;

namespace Credify.Commands;

[CommandCategory("Admin")]
public class WheelSimulateCommand : Command
{
    private readonly CredifyConfiguration _credifyConfig;
    private readonly WheelService _wheelService;

    public WheelSimulateCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        CredifyConfiguration credifyConfig, WheelService wheelService) 
        : base(config, translationLookup)
    {
        _credifyConfig = credifyConfig;
        _wheelService = wheelService;
        Name = "credifywheelsim";
        Alias = "crwheelsim";
        Description = "Simulates wheel of fortune spins for statistical analysis (Owner only)";
        Permission = EFClient.Permission.Owner;
        RequiresTarget = false;
        Arguments = [];
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        const int playerCount = 100_000;
        const int spinsPerPlayer = 1_000;
        const long startingBalance = 100; // Starting balance for each simulated player

        gameEvent.Origin.Tell($"Starting simulation: {playerCount:N0} players, {spinsPerPlayer:N0} spins each...");
        Console.WriteLine($"[Wheel Simulation] Starting: {playerCount:N0} players × {spinsPerPlayer:N0} spins = {playerCount * spinsPerPlayer:N0} total spins");

        var startTime = DateTime.UtcNow;

        // Thread-safe collections for aggregating results
        var segmentCounts = new ConcurrentDictionary<string, long>();
        var totalProfit = new ConcurrentDictionary<int, long>(); // Player index -> total profit
        var segmentProfits = new ConcurrentDictionary<string, long>(); // Segment name -> total profit
        var finalBalances = new ConcurrentBag<long>();

        // Parallel simulation
        await Parallel.ForEachAsync(
            Enumerable.Range(0, playerCount),
            new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
            async (playerIndex, ct) =>
            {
                var random = new Random(playerIndex); // Use player index as seed for reproducibility
                var balance = startingBalance;
                var playerProfit = 0L;

                for (int spin = 0; spin < spinsPerPlayer; spin++)
                {
                    // Get adjusted segments based on current balance
                    var segments = _wheelService.GetAdjustedSegments(balance);

                    // Spin the wheel
                    var segment = WheelService.SpinWheel(segments, random);

                    // Track segment hit
                    segmentCounts.AddOrUpdate(segment.Name, 1, (key, value) => value + 1);

                    // Calculate bet (full balance)
                    var bet = balance;
                    var originalBalance = balance;

                    // Deduct bet upfront (simulating the actual command behavior)
                    balance = 0;

                    // Calculate payout based on original balance
                    var payout = _wheelService.CalculatePayout(segment, originalBalance);

                    // Apply payout or loss
                    if (payout > 0)
                    {
                        balance = payout;
                    }
                    else if (payout < 0)
                    {
                        // Percentage loss - apply additional loss
                        // Balance can't go negative, so it stays at 0
                        balance = 0;
                    }
                    else
                    {
                        // Break even - restore the bet amount
                        balance = bet;
                    }

                    // Ensure balance never goes negative (safety check)
                    if (balance < 0) balance = 0;

                    // Calculate profit for this spin
                    var spinProfit = balance - originalBalance;
                    playerProfit += spinProfit;
                    segmentProfits.AddOrUpdate(segment.Name, spinProfit, (key, value) => value + spinProfit);
                }

                totalProfit[playerIndex] = playerProfit;
                finalBalances.Add(balance);
            });

        var endTime = DateTime.UtcNow;
        var duration = endTime - startTime;

        // Calculate statistics
        var totalSpins = (long)playerCount * spinsPerPlayer;
        var totalStartingBalance = (long)playerCount * startingBalance;
        var totalFinalBalance = finalBalances.Sum();
        var totalNetProfit = totalFinalBalance - totalStartingBalance;
        var averageFinalBalance = finalBalances.Average();
        var averageProfit = totalProfit.Values.Average();
        var playersProfitable = totalProfit.Values.Count(p => p > 0);
        var playersBroke = finalBalances.Count(b => b == 0);
        var maxBalance = finalBalances.Max();
        var minBalance = finalBalances.Min();

        // Print results to console
        Console.WriteLine();
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine("WHEEL OF FORTUNE SIMULATION RESULTS");
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine($"Simulation Duration: {duration.TotalSeconds:F2} seconds");
        Console.WriteLine($"Total Spins: {totalSpins:N0}");
        Console.WriteLine();
        Console.WriteLine("--- OVERALL STATISTICS ---");
        Console.WriteLine($"Starting Balance (per player): {startingBalance:N0}");
        Console.WriteLine($"Total Starting Balance: {totalStartingBalance:N0}");
        Console.WriteLine($"Total Final Balance: {totalFinalBalance:N0}");
        var profitPercentage = totalStartingBalance > 0 
            ? $"{totalNetProfit * 100.0 / totalStartingBalance:F2}%" 
            : "N/A";
        Console.WriteLine($"Total Net Profit/Loss: {totalNetProfit:N0} ({profitPercentage})");
        Console.WriteLine($"Average Final Balance: {averageFinalBalance:N2}");
        Console.WriteLine($"Average Profit per Player: {averageProfit:N2}");
        Console.WriteLine($"Players with Profit: {playersProfitable:N0} ({playersProfitable * 100.0 / playerCount:F2}%)");
        Console.WriteLine($"Players Broke (0 balance): {playersBroke:N0} ({playersBroke * 100.0 / playerCount:F2}%)");
        Console.WriteLine($"Max Final Balance: {maxBalance:N0}");
        Console.WriteLine($"Min Final Balance: {minBalance:N0}");
        Console.WriteLine();

        // Segment statistics
        Console.WriteLine("--- SEGMENT STATISTICS ---");
        var sortedSegments = segmentCounts.OrderByDescending(kvp => kvp.Value).ToList();
        foreach (var (segmentName, count) in sortedSegments)
        {
            var percentage = count * 100.0 / totalSpins;
            var totalSegmentProfit = segmentProfits.GetValueOrDefault(segmentName, 0);
            var avgProfitPerHit = count > 0 ? totalSegmentProfit / (double)count : 0;
            Console.WriteLine($"{segmentName,-20} | Hits: {count,12:N0} ({percentage,6:F2}%) | Total Profit: {totalSegmentProfit,15:N0} | Avg Profit/Hit: {avgProfitPerHit,15:N2}");
        }
        Console.WriteLine();

        // Profit distribution
        Console.WriteLine("--- PROFIT DISTRIBUTION ---");
        var profitRanges = new (long min, long max, string label)[]
        {
            (long.MinValue, -1_000_000, "< -1M"),
            (-1_000_000, -100_000, "-1M to -100K"),
            (-100_000, -10_000, "-100K to -10K"),
            (-10_000, 0, "-10K to 0"),
            (0, 10_000, "0 to 10K"),
            (10_000, 100_000, "10K to 100K"),
            (100_000, 1_000_000, "100K to 1M"),
            (1_000_000, long.MaxValue, "> 1M")
        };

        foreach (var range in profitRanges)
        {
            var count = totalProfit.Values.Count(p => p >= range.min && p < range.max);
            var percentage = count * 100.0 / playerCount;
            Console.WriteLine($"{range.label,-20} | Players: {count,10:N0} ({percentage,6:F2}%)");
        }
        Console.WriteLine("=".PadRight(80, '='));
        Console.WriteLine();

        gameEvent.Origin.Tell($"Simulation complete! Results printed to console. Duration: {duration.TotalSeconds:F2}s");
    }

}

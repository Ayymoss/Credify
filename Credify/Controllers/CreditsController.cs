using System.Net.Mime;
using System.Text.Json;
using Credify.Chat.Passive.Quests;
using Credify.Chat.Passive.Quests.Models;
using Credify.Constants;
using Credify.Services;
using Data.Abstractions;
using Data.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace Credify.Controllers;

/// <summary>
/// Credify economy API — balances, adjustments, quests, and leaderboards.
/// All endpoints require the Credify plugin; without it every route returns 404.
/// Reads are public (same data the webfront pages show); the balance adjustment is a write and
/// requires an authenticated Administrator (or higher) session.
/// </summary>
[ApiController]
[Route("api/credits")]
[Tags("Credify")]
[Produces(MediaTypeNames.Application.Json)]
public class CreditsController(
    ILogger<CreditsController> logger,
    IServiceProvider serviceProvider) : ControllerBase
{
    private const int MaxBatchSize = 100;
    private const int MaxLeaderboardSize = 100;

    private static readonly JsonSerializerOptions MetaJsonOptions = new() { PropertyNameCaseInsensitive = true };

    // resolved lazily so the controller (registered with the assembly) degrades to 404s if the
    // plugin's services failed to register — same pattern as the premium ZombieStatsController
    private readonly PersistenceService? _persistence = serviceProvider.GetService<PersistenceService>();
    private readonly QuestManager? _questManager = serviceProvider.GetService<QuestManager>();
    private readonly CreditsApiIdempotencyCache? _idempotencyCache = serviceProvider.GetService<CreditsApiIdempotencyCache>();
    private readonly IManager? _manager = serviceProvider.GetService<IManager>();
    private readonly IEntityService<EFClient>? _clientService = serviceProvider.GetService<IEntityService<EFClient>>();
    private readonly IDatabaseContextFactory? _contextFactory = serviceProvider.GetService<IDatabaseContextFactory>();

    /// <remarks>Returns one player's credit balance. A player with no credit history has balance 0.</remarks>
    /// <response code="200">Balance returned.</response>
    /// <response code="404">Credify is not installed, or no such client exists.</response>
    [HttpGet("balance/{clientId:int}")]
    [ProducesResponseType<ClientBalanceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBalance(int clientId)
    {
        if (_persistence is null)
        {
            return NotFound();
        }

        var client = await ResolveClientAsync(clientId);
        if (client is null)
        {
            return NotFound();
        }

        var balance = await _persistence.GetClientCreditsAsync(client);
        return Ok(new ClientBalanceDto(clientId, balance));
    }

    /// <remarks>
    /// Batch balance lookup. <c>clientIds</c> is comma-separated (e.g. <c>?clientIds=101,102,103</c>),
    /// capped at 100 ids. Unknown client ids are omitted from the response.
    /// </remarks>
    /// <response code="200">Balances returned.</response>
    /// <response code="400">clientIds is missing, malformed, or over the cap.</response>
    /// <response code="404">Credify is not installed.</response>
    [HttpGet("balances")]
    [ProducesResponseType<List<ClientBalanceDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBalances([FromQuery] string? clientIds)
    {
        if (_persistence is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(clientIds))
        {
            return BadRequest(new { error = "clientIds query parameter is required (comma-separated client ids)" });
        }

        var parts = clientIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var ids = new List<int>(parts.Length);
        foreach (var part in parts)
        {
            if (!int.TryParse(part, out var id))
            {
                return BadRequest(new { error = $"'{part}' is not a valid client id" });
            }

            ids.Add(id);
        }

        ids = ids.Distinct().ToList();
        if (ids.Count is 0 or > MaxBatchSize)
        {
            return BadRequest(new { error = $"between 1 and {MaxBatchSize} client ids are accepted" });
        }

        var result = new List<ClientBalanceDto>(ids.Count);
        foreach (var id in ids)
        {
            var client = await ResolveClientAsync(id);
            if (client is null)
            {
                continue;
            }

            result.Add(new ClientBalanceDto(id, await _persistence.GetClientCreditsAsync(client)));
        }

        return Ok(result);
    }

    /// <remarks>
    /// Applies a signed credit delta atomically: negative = deduct, positive = credit. By default a
    /// deduction that would overdraw is rejected with 409 and nothing changes (set
    /// <c>allowOverdraft</c> to force it). An optional <c>Idempotency-Key</c> header makes retries
    /// safe: a repeat of the same key for the same client within 10 minutes returns the recorded
    /// outcome instead of applying the delta again. The adjustment is audit-logged with the calling
    /// user and the supplied reason; it does not advance spend-credits quests.
    /// </remarks>
    /// <response code="200">Delta applied (or idempotent replay); new balance returned.</response>
    /// <response code="400">Amount is zero or the body is malformed.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="403">Authenticated below Administrator.</response>
    /// <response code="404">Credify is not installed, or no such client exists.</response>
    /// <response code="409">Deduction would overdraw the balance and allowOverdraft is false.</response>
    [HttpPost("{clientId:int}/adjust")]
    [Authorize(Roles = "Administrator,SeniorAdmin,Owner,Console")]
    [ProducesResponseType<ClientBalanceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AdjustBalance(
        int clientId,
        [FromBody] AdjustCreditsRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
    {
        if (_persistence is null || _idempotencyCache is null)
        {
            return NotFound();
        }

        if (request.Amount is 0)
        {
            return BadRequest(new { error = "amount must be a non-zero signed delta" });
        }

        if (!string.IsNullOrWhiteSpace(idempotencyKey) &&
            _idempotencyCache.TryGet(clientId, idempotencyKey, out var replayedBalance))
        {
            return Ok(new ClientBalanceDto(clientId, replayedBalance));
        }

        var client = await ResolveClientAsync(clientId);
        if (client is null)
        {
            return NotFound();
        }

        var (applied, balance) = await _persistence.TryAdjustCreditsAsync(client, request.Amount, request.AllowOverdraft);
        if (!applied)
        {
            return Conflict(new
            {
                error = "deduction would overdraw the balance; set allowOverdraft to force it",
                clientId,
                balance,
                requestedAmount = request.Amount
            });
        }

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            _idempotencyCache.Store(clientId, idempotencyKey, balance);
        }

        logger.LogInformation(
            "Credits adjusted via API: client {ClientId} delta {Delta} -> balance {Balance} by {User} (reason: {Reason})",
            clientId, request.Amount, balance, User.Identity?.Name ?? "unknown", request.Reason ?? "none");

        return Ok(new ClientBalanceDto(clientId, balance));
    }

    /// <remarks>
    /// Returns the currently active quest pool (the daily selection plus permanent quests).
    /// <c>id</c> is the stable objective code; <c>questId</c> is its numeric form as stored in
    /// player progress.
    /// </remarks>
    /// <response code="200">Active quest definitions returned.</response>
    /// <response code="404">Credify is not installed.</response>
    [HttpGet("quests")]
    [ProducesResponseType<List<QuestDefinitionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult GetQuestDefinitions()
    {
        if (_questManager is null)
        {
            return NotFound();
        }

        return Ok(ActiveQuestsSnapshot().Select(ToDefinitionDto).ToList());
    }

    /// <remarks>
    /// Returns one player's progress against the currently active quest pool. For an in-game player
    /// this is live progress; for an offline player it is the state persisted at their last
    /// disconnect. Rewards are granted automatically on completion, so <c>claimed</c> mirrors
    /// <c>completed</c>.
    /// </remarks>
    /// <response code="200">Quest progress returned.</response>
    /// <response code="404">Credify is not installed, or no such client exists.</response>
    [HttpGet("{clientId:int}/quests")]
    [ProducesResponseType<List<ClientQuestDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetClientQuests(int clientId)
    {
        if (_questManager is null || _persistence is null)
        {
            return NotFound();
        }

        var client = await ResolveClientAsync(clientId);
        if (client is null)
        {
            return NotFound();
        }

        // live clients get the manager's view (applies the daily-reset bookkeeping); offline
        // clients get their persisted state from the last disconnect
        var progress = client.IsIngame
            ? _questManager.GetPlayerQuests(client)
            : await _persistence.ReadClientQuestsAsync(client);

        var result = ActiveQuestsSnapshot()
            .Select(quest =>
            {
                var meta = progress.FirstOrDefault(p => p.QuestId == (int)quest.ObjectiveType);
                return new ClientQuestDto(
                    quest.ObjectiveType.ToString(),
                    (int)quest.ObjectiveType,
                    Math.Min(meta?.Progress ?? 0, quest.ObjectiveCount),
                    quest.ObjectiveCount,
                    meta?.Completed ?? false,
                    meta?.Completed ?? false);
            })
            .ToList();

        return Ok(result);
    }

    /// <remarks>
    /// Richest players, ranked by persisted credit balance. <c>limit</c> defaults to 25, capped at
    /// 100. Console/system accounts are excluded.
    /// </remarks>
    /// <response code="200">Leaderboard returned.</response>
    /// <response code="404">Credify is not installed.</response>
    [HttpGet("leaderboard")]
    [ProducesResponseType<List<CreditsLeaderboardEntryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLeaderboard([FromQuery] int limit = 25)
    {
        if (_persistence is null || _contextFactory is null)
        {
            return NotFound();
        }

        limit = Math.Clamp(limit, 1, MaxLeaderboardSize);

        await using var context = _contextFactory.CreateContext(false);
        // balances are persisted as numeric strings; for non-negative integers without leading
        // zeros, (length desc, value desc) is exact numeric ordering and translates to SQL —
        // negative balances are excluded (they can't be top entries anyway)
        var rows = await context.Set<EFMeta>()
            .Where(meta => meta.Key == PluginConstants.CreditsAmount && meta.ClientId > 1)
            .Where(meta => !meta.Value.StartsWith("-"))
            .OrderByDescending(meta => meta.Value.Length)
            .ThenByDescending(meta => meta.Value)
            .Take(limit)
            .Select(meta => new
            {
                ClientId = meta.ClientId!.Value,
                meta.Value,
                meta.Client.CurrentAlias.Name
            })
            .ToListAsync();

        var result = rows
            .Select((row, index) => new CreditsLeaderboardEntryDto(
                index + 1,
                row.ClientId,
                row.Name.StripColors(),
                long.TryParse(row.Value, out var balance) ? balance : 0))
            .ToList();

        return Ok(result);
    }

    /// <remarks>
    /// Players ranked by how many quests they currently have completed. Point-in-time view: daily
    /// quest completions reset each day and repeatable quests reset on completion, so this reflects
    /// the present state, not all-time totals. <c>limit</c> defaults to 25, capped at 100.
    /// </remarks>
    /// <response code="200">Quest leaderboard returned.</response>
    /// <response code="404">Credify is not installed.</response>
    [HttpGet("leaderboard/quests")]
    [ProducesResponseType<List<QuestLeaderboardEntryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetQuestLeaderboard([FromQuery] int limit = 25)
    {
        if (_questManager is null || _contextFactory is null)
        {
            return NotFound();
        }

        limit = Math.Clamp(limit, 1, MaxLeaderboardSize);

        await using var context = _contextFactory.CreateContext(false);
        // quest progress is one JSON blob per client; completion counts can't be ranked in SQL, so
        // pull the (small) blobs for everyone who has quest state and rank in memory
        var rows = await context.Set<EFMeta>()
            .Where(meta => meta.Key == PluginConstants.ClientQuestsKey && meta.ClientId > 1)
            .Where(meta => meta.Value != "[]")
            .Select(meta => new
            {
                ClientId = meta.ClientId!.Value,
                meta.Value,
                meta.Client.CurrentAlias.Name
            })
            .ToListAsync();

        var ranked = rows
            .Select(row => new
            {
                row.ClientId,
                row.Name,
                CompletedQuests = CountCompletedQuests(row.Value)
            })
            .Where(row => row.CompletedQuests > 0)
            .OrderByDescending(row => row.CompletedQuests)
            .ThenBy(row => row.ClientId)
            .Take(limit)
            .Select((row, index) => new QuestLeaderboardEntryDto(
                index + 1, row.ClientId, row.Name.StripColors(), row.CompletedQuests))
            .ToList();

        return Ok(ranked);
    }

    private int CountCompletedQuests(string serializedQuests)
    {
        try
        {
            var quests = JsonSerializer.Deserialize<List<QuestMeta>>(serializedQuests, MetaJsonOptions);
            return quests?.Count(quest => quest.Completed) ?? 0;
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Unparseable quest meta encountered while building the quest leaderboard");
            return 0;
        }
    }

    // ActiveQuests is replaced wholesale by the daily regeneration; snapshot so enumeration is safe
    private List<Quest> ActiveQuestsSnapshot() => [.._questManager!.ActiveQuests];

    private static QuestDefinitionDto ToDefinitionDto(Quest quest) => new(
        quest.ObjectiveType.ToString(),
        (int)quest.ObjectiveType,
        quest.Name,
        $"Reach {quest.ObjectiveCount:N0} — {quest.ObjectiveType}",
        quest.Reward,
        quest.ObjectiveCount,
        quest.IsPermanent,
        quest.IsRepeatable);

    /// <summary>
    /// Resolves a client id to the runtime client: the live in-game instance when connected (so
    /// in-memory credit/quest state is used), else the persisted client. Null when no such client
    /// exists.
    /// </summary>
    private async Task<EFClient?> ResolveClientAsync(int clientId)
    {
        if (clientId <= 0 || _manager is null || _clientService is null)
        {
            return null;
        }

        var live = _manager.GetActiveClients().FirstOrDefault(c => c.ClientId == clientId);
        if (live is not null)
        {
            return live;
        }

        return await _clientService.Get(clientId);
    }
}

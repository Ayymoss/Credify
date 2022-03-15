using Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace CreditsPlugin.Commands;

public class TopCreditsCommand : Command
{
    public TopCreditsCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        IDatabaseContextFactory contextFactory) :
        base(config, translationLookup)
    {
        _contextFactory = contextFactory;
        Name = "topcredits";
        Alias = "topcr";
        Description = "List top 5 players with most credits.";
        Permission = EFClient.Permission.User;
        RequiresTarget = false;
    }

    private readonly IDatabaseContextFactory _contextFactory;

    public override async Task ExecuteAsync(GameEvent e)
    {
        if (e.Type != GameEvent.EventType.Command) return;
        
        // If user requests top and there are no entries.
        if (!TopCreditsLogic.TopCredits.Any())
        {
            e.Origin.Tell("There are no users in the top.");
            return;
        }
        
        e.Origin.Tell($"(Color::Yellow)Top Credits:");

        // Get top credits, format for returning.
        await using var context = _contextFactory.CreateContext(false);
        var names = await context.Clients
            .Where(client => TopCreditsLogic.TopCredits.Select(credit => credit.ClientId).Contains(client.ClientId))
            .Select(client => new {client.ClientId, client.CurrentAlias.Name})
            .ToDictionaryAsync(selector => selector.ClientId, selector => selector.Name);

        var output = TopCreditsLogic.TopCredits.OrderByDescending(entry => entry.Credits).Select((creditEntry, index) =>
            $"#{index + 1} {names[creditEntry.ClientId]} (Color::White)- {creditEntry.Credits}");

        e.Origin.Tell(output);
    }
}
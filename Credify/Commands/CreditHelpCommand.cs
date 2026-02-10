using Credify.Commands.Attributes;
using Credify.Configuration;
using Credify.Services;
using Data.Models.Client;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using EFClient = Data.Models.Client.EFClient;

namespace Credify.Commands;

[CommandCategory("Other")]
public class CreditHelpCommand : Command
{
    private readonly CredifyConfiguration _credifyConfig;
    private readonly CommandDiscoveryService _commandDiscoveryService;

    public CreditHelpCommand(CommandConfiguration config, ITranslationLookup layout, CredifyConfiguration credifyConfig,
        CommandDiscoveryService commandDiscoveryService) : base(config, layout)
    {
        _credifyConfig = credifyConfig;
        _commandDiscoveryService = commandDiscoveryService;
        Name = "credifyhelp";
        Description = credifyConfig.Translations.Core.CommandHelpDescription;
        Alias = "crhelp";
        Permission = EFClient.Permission.User;
        RequiresTarget = false;
        Arguments =
        [
            new CommandArgument
            {
                Name = "Category",
                Required = false
            }
        ];
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        // Set manager if available
        if (gameEvent.Owner?.Manager != null)
        {
            _commandDiscoveryService.SetManager(gameEvent.Owner.Manager);
        }

        // Get user's permission level for filtering
        var userPermission = gameEvent.Origin.Level;
        
        var categoryArg = gameEvent.Data?.Trim().ToLower();
        var commandsByCategory = _commandDiscoveryService.GetCommandsByCategory(userPermission);
        var categories = _commandDiscoveryService.GetCategories(userPermission);

        var messages = new List<string>();

        if (string.IsNullOrWhiteSpace(categoryArg))
        {
            // Show all categories
            messages.Add(_credifyConfig.Translations.Core.HelpHeader);
            messages.Add(_credifyConfig.Translations.Core.HelpAvailableCategories);
            
            foreach (var category in categories)
            {
                var commandCount = commandsByCategory[category].Count;
                messages.Add($"  (Color::Accent){category} (Color::White)({commandCount} commands)");
            }
            
            messages.Add(_credifyConfig.Translations.Core.HelpCategoryUsage);
        }
        else
        {
            // Show commands in specific category
            var category = categories.FirstOrDefault(c => 
                c.Equals(categoryArg, StringComparison.OrdinalIgnoreCase) ||
                _commandDiscoveryService.GetCategoryDisplayName(c).Equals(categoryArg, StringComparison.OrdinalIgnoreCase));

            if (category == null || !commandsByCategory.TryGetValue(category, out var commands))
            {
                messages.Add(_credifyConfig.Translations.Core.HelpUnknownCategory.FormatExt(categoryArg));
                messages.Add(_credifyConfig.Translations.Core.HelpAvailableCategories);
                foreach (var cat in categories)
                {
                    messages.Add($"  (Color::Accent){cat}");
                }
            }
            else
            {
                var displayName = _commandDiscoveryService.GetCategoryDisplayName(category);
                messages.Add(_credifyConfig.Translations.Core.HelpCategoryHeader.FormatExt(displayName));

                foreach (var cmd in commands)
                {
                    var aliasDisplay = string.IsNullOrWhiteSpace(cmd.Alias) 
                        ? "" 
                        : $"(Color::Yellow)!{cmd.Alias}";
                    var description = string.IsNullOrWhiteSpace(cmd.Description) 
                        ? "" 
                        : $" (Color::White){cmd.Description}";
                    
                    if (!string.IsNullOrWhiteSpace(aliasDisplay))
                    {
                        messages.Add($"[{aliasDisplay}(Color::White)]{description}");
                    }
                    else if (!string.IsNullOrWhiteSpace(cmd.Name))
                    {
                        messages.Add($"[{cmd.Name}]{description}");
                    }
                }
            }
        }

        await gameEvent.Origin.TellAsync(messages);
    }
}

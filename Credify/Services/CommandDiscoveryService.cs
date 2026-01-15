using System.Reflection;
using Credify.Commands;
using Credify.Commands.Attributes;
using Credify.Configuration;
using Data.Models.Client;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Services;

/// <summary>
/// Service for discovering and categorizing commands dynamically.
/// </summary>
public class CommandDiscoveryService(CredifyConfiguration credifyConfig)
{
    private readonly Dictionary<string, List<CommandInfo>> _commandsByCategory = new();

    private readonly Dictionary<string, string> _categoryDisplayNames = new()
    {
        ["Games"] = "Games",
        ["Shop"] = "Shop",
        ["Credits"] = "Credits",
        ["Quests"] = "Quests",
        ["Raffle"] = "Raffle",
        ["Bounties"] = "Bounties",
        ["Admin"] = "Admin",
        ["Other"] = "Other"
    };

    private IManager? _manager;
    private bool _initialized = false;

    /// <summary>
    /// Sets the manager instance for command discovery.
    /// </summary>
    public void SetManager(IManager manager)
    {
        // Only reset if manager changed to avoid unnecessary rediscovery
        if (_manager != manager)
        {
            _manager = manager;
            _initialized = false; // Reset to rediscover with manager
        }
    }

    /// <summary>
    /// Gets all commands grouped by category, filtered by user permission level.
    /// </summary>
    public Dictionary<string, List<CommandInfo>> GetCommandsByCategory(EFClient.Permission? userPermission = null)
    {
        if (!_initialized)
        {
            DiscoverCommands();
            _initialized = true;
        }

        if (userPermission == null)
        {
            return _commandsByCategory;
        }

        // Filter commands by permission level
        var filtered = new Dictionary<string, List<CommandInfo>>();
        foreach (var category in _commandsByCategory.Keys)
        {
            var accessibleCommands = _commandsByCategory[category]
                .Where(cmd => HasPermission(userPermission.Value, cmd.RequiredPermission))
                .ToList();

            if (accessibleCommands.Count > 0)
            {
                filtered[category] = accessibleCommands;
            }
        }

        return filtered;
    }

    /// <summary>
    /// Gets all available categories, filtered by user permission level.
    /// </summary>
    public List<string> GetCategories(EFClient.Permission? userPermission = null)
    {
        if (!_initialized)
        {
            DiscoverCommands();
            _initialized = true;
        }

        if (userPermission == null)
        {
            return _commandsByCategory.Keys.OrderBy(k => k).ToList();
        }

        // Only return categories that have at least one accessible command
        return GetCommandsByCategory(userPermission).Keys.OrderBy(k => k).ToList();
    }

    /// <summary>
    /// Checks if a user's permission level is sufficient for a command.
    /// </summary>
    private static bool HasPermission(EFClient.Permission userLevel, EFClient.Permission requiredLevel)
    {
        // Permission enum is ordered: User < Moderator < Administrator < Owner
        // Higher enum values have more permissions
        return userLevel >= requiredLevel;
    }

    /// <summary>
    /// Gets display name for a category.
    /// </summary>
    public string GetCategoryDisplayName(string category)
    {
        return _categoryDisplayNames.TryGetValue(category, out var displayName)
            ? displayName
            : category;
    }

    private void DiscoverCommands()
    {
        // Clear existing commands to prevent duplicates when rediscovering
        _commandsByCategory.Clear();

        var commandTypes = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => t.IsSubclassOf(typeof(Command)) &&
                        t is { Namespace: "Credify.Commands", IsAbstract: false } &&
                        t != typeof(CreditHelpCommand)) // Exclude help command itself
            .ToList();

        foreach (var commandType in commandTypes)
        {
            try
            {
                var commandInfo = ExtractCommandInfo(commandType);
                if (commandInfo == null) continue;

                var category = DetermineCategory(commandType);
                commandInfo.Category = category;

                if (!_commandsByCategory.ContainsKey(category))
                {
                    _commandsByCategory[category] = [];
                }

                _commandsByCategory[category].Add(commandInfo);
            }
            catch
            {
                // Skip commands that can't be instantiated or analyzed
                continue;
            }
        }

        // Sort commands within each category by display order, then by name
        foreach (var category in _commandsByCategory.Keys)
        {
            _commandsByCategory[category] = _commandsByCategory[category]
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.Name)
                .ToList();
        }
    }

    private CommandInfo? ExtractCommandInfo(Type commandType)
    {
        // Try to get command instance from manager if available (best source of truth)
        Command? commandInstance = null;
        if (_manager != null)
        {
            try
            {
                // Try multiple ways to get commands from manager
                var commandsProperty = _manager.GetType().GetProperty("Commands");
                if (commandsProperty != null)
                {
                    var commands = commandsProperty.GetValue(_manager) as IEnumerable<Command>;
                    if (commands != null)
                    {
                        commandInstance = commands.FirstOrDefault(c => c.GetType() == commandType);
                    }
                }

                // Try alternative: GetCommands method
                if (commandInstance == null)
                {
                    var getCommandsMethod = _manager.GetType().GetMethod("GetCommands");
                    if (getCommandsMethod != null)
                    {
                        var commands = getCommandsMethod.Invoke(_manager, null) as IEnumerable<Command>;
                        if (commands != null)
                        {
                            commandInstance = commands.FirstOrDefault(c => c.GetType() == commandType);
                        }
                    }
                }
            }
            catch
            {
                // If manager doesn't expose commands, fall back to reflection
            }
        }

        int displayOrder = 0;
        var categoryAttr = commandType.GetCustomAttribute<CommandCategoryAttribute>();
        if (categoryAttr != null)
        {
            displayOrder = categoryAttr.DisplayOrder;
        }

        if (commandInstance != null)
        {
            // Use actual command instance properties (most reliable)
            return new CommandInfo
            {
                Name = commandInstance.Name,
                Alias = commandInstance.Alias,
                Description = commandInstance.Description,
                RequiredPermission = commandInstance.Permission,
                DisplayOrder = displayOrder
            };
        }

        // Fallback: Instantiate via reflection to read actual properties
        return ExtractCommandInfoFromConstructor(commandType, displayOrder);
    }

    /// <summary>
    /// Extracts command metadata by instantiating the command via reflection.
    /// This is the only way to get actual Name, Alias, Description, Permission values
    /// since they can be changed in configuration files.
    /// </summary>
    private CommandInfo? ExtractCommandInfoFromConstructor(Type commandType, int displayOrder)
    {
        try
        {
            // Get the constructor with the most parameters (usually the DI constructor)
            var constructor = commandType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .OrderByDescending(c => c.GetParameters().Length)
                .FirstOrDefault();

            if (constructor == null)
            {
                return null;
            }

            // Prepare parameters for the constructor
            var parameters = constructor.GetParameters();
            var args = new object[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                var paramType = parameters[i].ParameterType;

                // Use actual CredifyConfiguration instance we have
                if (paramType == typeof(CredifyConfiguration))
                {
                    args[i] = credifyConfig;
                }
                // Create minimal instances for common types
                else if (paramType == typeof(CommandConfiguration))
                {
                    args[i] = new CommandConfiguration();
                }
                else if (paramType.IsInterface || paramType.IsAbstract)
                {
                    // For interfaces/abstract classes, pass null
                    // The constructor might access properties, but we'll catch exceptions
                    args[i] = null;
                }
                else if (paramType.IsValueType)
                {
                    args[i] = Activator.CreateInstance(paramType);
                }
                else
                {
                    // Try to create instance for concrete classes
                    try
                    {
                        args[i] = Activator.CreateInstance(paramType);
                    }
                    catch
                    {
                        args[i] = null;
                    }
                }
            }

            // Instantiate the command - this will run the constructor and set Name, Alias, Description, Permission
            var instance = (Command)constructor.Invoke(args);

            // Read the properties directly from the instantiated command
            // These are the ACTUAL values, not guessed patterns
            return new CommandInfo
            {
                Name = instance.Name,
                Alias = instance.Alias,
                Description = instance.Description,
                RequiredPermission = instance.Permission,
                DisplayOrder = displayOrder
            };
        }
        catch (TargetInvocationException)
        {
            // Constructor threw an exception (likely due to null dependencies)
            // Cannot extract command info without instantiation - return null
            // We cannot guess Name/Alias as they can be changed in config files
            return null;
        }
        catch
        {
            // Any other error - cannot extract command info
            return null;
        }
    }

    private string DetermineCategory(Type commandType)
    {
        // Check for explicit category attribute
        var categoryAttr = commandType.GetCustomAttribute<CommandCategoryAttribute>();
        return categoryAttr != null ? categoryAttr.Category : "Other";
    }
}

/// <summary>
/// Information about a command for help display.
/// </summary>
public class CommandInfo
{
    public string Name { get; set; } = "";
    public string Alias { get; set; } = "";
    public string Description { get; set; } = "";
    public string Category { get; set; } = "";
    public EFClient.Permission RequiredPermission { get; set; } = EFClient.Permission.User;
    public int DisplayOrder { get; set; } = 0;
}

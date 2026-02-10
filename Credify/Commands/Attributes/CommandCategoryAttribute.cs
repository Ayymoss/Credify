namespace Credify.Commands.Attributes;

/// <summary>
/// Attribute to explicitly categorize a command for help display.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class CommandCategoryAttribute(string category) : Attribute
{
    /// <summary>
    /// The category name for this command.
    /// </summary>
    public string Category { get; } = category;

    /// <summary>
    /// Display order within the category (lower numbers appear first).
    /// </summary>
    public int DisplayOrder { get; set; } = 0;
}

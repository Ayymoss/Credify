using Microsoft.AspNetCore.Components;

namespace Credify.Components.Shared;

public partial class StatCard
{
    [Parameter, EditorRequired] public string Title { get; set; } = "";
    [Parameter, EditorRequired] public string Value { get; set; } = "";
}

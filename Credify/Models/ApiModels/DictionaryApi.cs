using System.Text.Json.Serialization;

namespace Credify.Models.ApiModels;

public class DictionaryApi
{
    [JsonPropertyName("meanings")] public List<MeaningModel> Meanings { get; set; }
}

public class DefinitionModel
{
    [JsonPropertyName("definition")] public string Definition { get; set; }
}

public class MeaningModel
{
    [JsonPropertyName("definitions")] public List<DefinitionModel> Definitions { get; set; }
}

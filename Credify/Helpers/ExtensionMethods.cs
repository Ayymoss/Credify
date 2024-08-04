using System.Text.Json;

namespace Credify.Helpers;

public static class ExtensionMethods
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<TResponse?> DeserializeHttpResponseContentAsync<TResponse>(
        this HttpResponseMessage response) where TResponse : class
    {
        try
        {
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync();
            return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<TResponse>(json, JsonSerializerOptions);
        }
        catch
        {
            return null;
        }
    }
}

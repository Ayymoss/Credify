using System.Text.Json;

namespace Credify.Helpers;

public static class ExtensionMethods
{
    public static async Task<TResponse?> DeserializeHttpResponseContentAsync<TResponse>(
        this HttpResponseMessage response) where TResponse : class
    {
        try
        {
            var jsonSerializerOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync();
            return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<TResponse>(json, jsonSerializerOptions);
        }
        catch
        {
            return null;
        }
    }
}

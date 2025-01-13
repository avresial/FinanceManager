using System.Text.Json;

namespace FinanceManager.Components.Helpers
{
    public static class JsonHelper
    {
        public static JsonSerializerOptions GetJsonSerializerOptions()
        {
            return new()
            {
                AllowTrailingCommas = true,
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Skip
            };
        }

        public static string SerializeObj<T>(T modelObject) => JsonSerializer.Serialize(modelObject, GetJsonSerializerOptions());
        public static StringContent GenerateStringContent(string serializeObj) => new(serializeObj, System.Text.Encoding.UTF8, "application/json");
    }
}

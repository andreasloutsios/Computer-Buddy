using System.Text.Json;
using System.Text.Json.Serialization;

namespace ComputerBuddy.Contracts;

public static class BuddyIpcJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}


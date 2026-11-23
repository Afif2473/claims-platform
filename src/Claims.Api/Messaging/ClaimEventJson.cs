using System.Text.Json;
using System.Text.Json.Serialization;

namespace Claims.Api.Messaging;

/// <summary>Shared JSON settings for the claim-events wire format (camelCase, enums as strings).</summary>
public static class ClaimEventJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}

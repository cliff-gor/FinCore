using System.Text.Json;
using System.Text.Json.Serialization;

namespace FinCore.BuildingBlocks.Infrastructure.EventBus;

internal static class OutboxSerializer
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}

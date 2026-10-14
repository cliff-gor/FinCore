using System.Text.Json;
using System.Text.Json.Serialization;
using FinCore.Modules.Identity.Domain.Staff;

namespace FinCore.Modules.Identity.Application.Approvals;

/// <summary>What a checker sees for a role grant: who gets which role.</summary>
public sealed record GrantRolePayload(Role Role, Guid StaffId, string StaffName, string StaffEmail);

/// <summary>What a checker sees for a limit raise: from and to, so the size of the change is obvious.</summary>
public sealed record RaiseLimitPayload(ApprovalLimitType LimitType, decimal CurrentLimit, decimal NewLimit, Guid StaffId, string StaffName);

internal static class IdentityPayloads
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize<T>(T payload) => JsonSerializer.Serialize(payload, Options);

    public static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options) ?? throw new InvalidOperationException($"Empty {typeof(T).Name} payload.");
}

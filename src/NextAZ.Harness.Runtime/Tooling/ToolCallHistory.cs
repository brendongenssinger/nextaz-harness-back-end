using System.Security.Cryptography;
using System.Text.Json;

namespace NextAZ.Harness.Runtime.Tooling;

// Owned by one execution, never registered as a singleton or shared across tenants.
internal sealed class ToolCallHistory
{
    private const int MaxArgumentCharacters = 16384;
    private const int MaxArgumentDepth = 64;
    private readonly HashSet<string> _callIds = new(StringComparer.Ordinal);
    private readonly HashSet<(string ToolId, string ArgumentsHash)> _calls = [];

    public bool TryRegister(ToolCall call)
    {
        if (string.IsNullOrWhiteSpace(call.CallId) || call.CallId.Length > 128 ||
            call.Arguments.ValueKind != JsonValueKind.Object ||
            call.Arguments.GetRawText().Length > MaxArgumentCharacters || _callIds.Contains(call.CallId))
            return false;

        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            if (!TryWriteCanonical(writer, call.Arguments, 0)) return false;
        }

        var fingerprint = (call.ToolId.Value, Convert.ToHexString(SHA256.HashData(buffer.ToArray())));
        if (!_calls.Add(fingerprint)) return false;
        _callIds.Add(call.CallId);
        return true;
    }

    private static bool TryWriteCanonical(Utf8JsonWriter writer, JsonElement value, int depth)
    {
        if (depth > MaxArgumentDepth) return false;
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    // Duplicate names are ambiguous (different consumers may pick different values).
                    if (!names.Add(property.Name)) return false;
                    writer.WritePropertyName(property.Name);
                    if (!TryWriteCanonical(writer, property.Value, depth + 1)) return false;
                }
                writer.WriteEndObject();
                return true;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray())
                    if (!TryWriteCanonical(writer, item, depth + 1)) return false;
                writer.WriteEndArray();
                return true;
            case JsonValueKind.String:
                writer.WriteStringValue(value.GetString());
                return true;
            default:
                // Keep number representations/types and array order; do not infer domain equivalence.
                value.WriteTo(writer);
                return true;
        }
    }
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NextAZ.Harness.Domain.Executions;
using HarnessContext = NextAZ.Harness.Domain.Executions.ExecutionContext;
namespace NextAZ.Harness.Infrastructure.Imobiliaria;
internal static class ImobiliariaToolArguments
{
    public static bool GuidProperty(JsonElement value, string property) =>
        value.TryGetProperty(property, out var item) && item.ValueKind == JsonValueKind.String &&
        Guid.TryParse(item.GetString(), out var id) && id != Guid.Empty;
    public static Guid GetGuid(JsonElement value, string property) => Guid.Parse(value.GetProperty(property).GetString()!);
    public static bool StringProperty(JsonElement value, string property, int max, bool required = true)
    {
        if (!value.TryGetProperty(property, out var item) || item.ValueKind == JsonValueKind.Null) return !required;
        return item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()) && item.GetString()!.Length <= max;
    }
    public static bool OptionalGuid(JsonElement value, string property) =>
        !value.TryGetProperty(property, out var item) || item.ValueKind == JsonValueKind.Null ||
        item.ValueKind == JsonValueKind.String && Guid.TryParse(item.GetString(), out var id) && id != Guid.Empty;
    public static bool DateProperty(JsonElement value, string property) =>
        value.TryGetProperty(property, out var item) && item.ValueKind == JsonValueKind.String &&
        DateTimeOffset.TryParse(item.GetString(), out _);
    public static Guid CommandId(HarnessContext context, string tool, JsonElement arguments)
    {
        var value = context.TenantId + ":" + context.CorrelationId + ":" + tool + ":" + Canonicalize(arguments);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return new Guid(hash.AsSpan(0, 16));
    }
    private static string Canonicalize(JsonElement value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) WriteCanonical(writer, value);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in value.EnumerateArray()) WriteCanonical(writer, item);
                writer.WriteEndArray();
                break;
            default:
                value.WriteTo(writer);
                break;
        }
    }
}

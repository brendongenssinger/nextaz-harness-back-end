using System.Text.Json;
namespace NextAZ.Harness.Runtime.Tooling;
public sealed record ToolResult(string CallId, ToolId ToolId, JsonElement Output, JsonElement Arguments = default);

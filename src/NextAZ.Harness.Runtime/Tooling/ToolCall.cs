using System.Text.Json;
namespace NextAZ.Harness.Runtime.Tooling;
public sealed record ToolCall(string CallId, ToolId ToolId, JsonElement Arguments);

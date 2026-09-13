using System.Text.Json;
namespace NextAZ.Harness.Runtime.Tooling;
public interface IToolHandler
{
    ToolDefinition Definition { get; }
    bool ValidateArguments(JsonElement arguments);
    Task<JsonElement> ExecuteAsync(HarnessContext context, JsonElement arguments, CancellationToken cancellationToken);
}

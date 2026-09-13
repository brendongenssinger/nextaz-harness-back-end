using System.Text.RegularExpressions;
namespace NextAZ.Harness.Domain.Tools;
public sealed record ToolId
{
    public string Value { get; }
    public ToolId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 120 ||
            !Regex.IsMatch(value, @"^[a-z][a-z0-9-]*(\.[a-z][a-z0-9-]*)+$"))
            throw new ArgumentException("Use a stable business capability identifier.", nameof(value));
        Value = value;
    }
    public override string ToString() => Value;
}

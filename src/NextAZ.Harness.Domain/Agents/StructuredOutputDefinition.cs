namespace NextAZ.Harness.Domain.Agents;

public sealed record StructuredOutputDefinition
{
    public string Name { get; }
    public string Description { get; }
    public string JsonSchema { get; }

    public StructuredOutputDefinition(string name, string description, string jsonSchema)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonSchema);
        if (name.Length > 64 || !name.All(character =>
                char.IsAsciiLetterOrDigit(character) || character is '_' or '-'))
            throw new ArgumentException("Structured output name must be an ASCII identifier up to 64 characters.",
                nameof(name));
        if (description.Length > 500)
            throw new ArgumentException("Structured output description is too long.", nameof(description));
        if (jsonSchema.Length > 16_384)
            throw new ArgumentException("Structured output schema is too large.", nameof(jsonSchema));

        Name = name;
        Description = description;
        JsonSchema = jsonSchema;
    }
}

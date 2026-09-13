namespace NextAZ.Harness.Domain.Policies;
public sealed record MemoryPolicy(string Name)
{
    public static MemoryPolicy None { get; } = new("none");
}

namespace NextAZ.Harness.Domain.Policies;
public sealed record ModelPolicy
{
    public string Name { get; }
    public ModelPolicy(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Length > 100) throw new ArgumentException("Policy name too long.", nameof(name));
        Name = name;
    }
}

namespace NextAZ.Harness.Domain.Agents;
public sealed record AgentVersion
{
    public int Value { get; }
    public AgentVersion(int value)
    {
        if (value < 1) throw new ArgumentOutOfRangeException(nameof(value));
        Value = value;
    }
}

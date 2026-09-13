namespace NextAZ.Harness.Domain.Executions;
public sealed record ExecutionId
{
    public Guid Value { get; }
    public ExecutionId(Guid value)
    {
        if (value == Guid.Empty) throw new ArgumentException("ExecutionId is required.", nameof(value));
        Value = value;
    }
    public static ExecutionId New() => new(Guid.NewGuid());
}

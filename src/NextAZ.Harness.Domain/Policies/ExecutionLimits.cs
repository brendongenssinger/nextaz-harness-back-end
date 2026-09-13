namespace NextAZ.Harness.Domain.Policies;
public sealed record ExecutionLimits
{
    public int MaxSteps { get; }
    public int MaxToolCalls { get; }
    public int MaxOutputTokens { get; }
    public TimeSpan Timeout { get; }
    public ExecutionLimits(int maxSteps = 4, int maxToolCalls = 4, int maxOutputTokens = 1024, int timeoutSeconds = 30)
    {
        if (maxSteps is < 1 or > 32 || maxToolCalls is < 0 or > 32 ||
            maxOutputTokens is < 1 or > 32768 || timeoutSeconds is < 1 or > 120)
            throw new ArgumentOutOfRangeException(nameof(maxSteps), "Invalid execution limits.");
        MaxSteps = maxSteps; MaxToolCalls = maxToolCalls;
        MaxOutputTokens = maxOutputTokens; Timeout = TimeSpan.FromSeconds(timeoutSeconds);
    }
}

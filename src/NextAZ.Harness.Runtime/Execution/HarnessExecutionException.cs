namespace NextAZ.Harness.Runtime.Execution;
public sealed class HarnessExecutionException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

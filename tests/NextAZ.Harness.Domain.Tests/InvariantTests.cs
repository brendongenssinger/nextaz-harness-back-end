using NextAZ.Harness.Domain.Agents;
using NextAZ.Harness.Domain.Executions;
using NextAZ.Harness.Domain.Policies;
using HarnessContext = NextAZ.Harness.Domain.Executions.ExecutionContext;
namespace NextAZ.Harness.Domain.Tests;
public sealed class InvariantTests
{
    [Theory]
    [InlineData("")] [InlineData("GPTAgent")] [InlineData("ClaudeAgent")]
    [InlineData("cartorio atendimento")] [InlineData("../tenant")]
    public void RejectsUnstructuredAgentIdentity(string value) =>
        Assert.Throws<ArgumentException>(() => new AgentId(value));
    [Fact] public void AcceptsStableBusinessIdentity() =>
        Assert.Equal(new AgentId("cartorio.atendimento"), new AgentId("cartorio.atendimento"));
    [Theory] [InlineData(0)] [InlineData(-1)]
    public void VersionMustBePositive(int version) => Assert.Throws<ArgumentOutOfRangeException>(() => new AgentVersion(version));
    [Fact] public void ExecutionMustHaveTenant() => Assert.Throws<ArgumentException>(() =>
        new HarnessContext(Guid.Empty, new("cartorio.atendimento"), new(1), ExecutionId.New(),
            "correlation", "api", "atendimento", DateTimeOffset.UtcNow));
    [Fact] public void RejectsUnboundedLoop() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExecutionLimits(maxSteps: 1000));
    [Fact] public void ExecutionIdMustBeNonEmpty() =>
        Assert.Throws<ArgumentException>(() => new ExecutionId(Guid.Empty));
    [Fact] public void DomainHasNoExternalFrameworkDependencies() =>
        Assert.All(typeof(AgentId).Assembly.GetReferencedAssemblies(), assembly =>
            Assert.True(assembly.Name!.StartsWith("System", StringComparison.Ordinal), assembly.Name));
}

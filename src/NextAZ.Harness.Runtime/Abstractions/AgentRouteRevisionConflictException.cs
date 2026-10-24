namespace NextAZ.Harness.Runtime.Abstractions;

public sealed class AgentRouteRevisionConflictException() : Exception("The agent route changed concurrently.");

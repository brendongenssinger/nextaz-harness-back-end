namespace NextAZ.Harness.Runtime.Tooling;
// Marker for adapters whose authoritative backend revalidates tenant, frozen arguments and durable idempotency.
public interface IBackendAuthorizedWriteToolHandler : IToolHandler;

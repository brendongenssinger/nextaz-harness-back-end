namespace NextAZ.Harness.Runtime.Memory;
public sealed record MemoryContext(Guid TenantId, Guid? KnowledgeBaseId, IReadOnlyList<string> Entries);

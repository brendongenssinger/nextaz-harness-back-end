namespace NextAZ.Harness.Api.Configuration;
public sealed record HarnessRuntimeDescriptor(string Mode, string Persistence, string Provider, string Messaging,
    bool UsesPostgres);

namespace NextAZ.Harness.Runtime.ModelRouting;
// Only transient, pre-response provider failures may trigger route fallback.
public sealed class ModelProviderUnavailableException() : Exception("Provider unavailable.");

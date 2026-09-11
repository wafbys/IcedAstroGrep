using Xunit;

// Grep and EncodingCache hold process wide state, and the regex timeout test measures wall clock
// time, so the suite runs sequentially to stay deterministic.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

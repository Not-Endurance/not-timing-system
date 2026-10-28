// The run stays within what the localhost can start at once (ADR-0016): at most four test threads here, and at most two
// browsers at a time, which PasskeyBrowserFixture holds.
[assembly: CollectionBehavior(MaxParallelThreads = 4)]

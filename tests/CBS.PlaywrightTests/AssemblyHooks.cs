using Xunit;

// Disable parallel test execution for Playwright E2E tests to ensure clean port isolation and deterministic state
[assembly: CollectionBehavior(DisableTestParallelization = true)]

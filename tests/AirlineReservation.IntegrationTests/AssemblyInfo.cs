using Xunit;

// Disable parallel execution across integration test classes sharing the local SQL Server database
[assembly: CollectionBehavior(DisableTestParallelization = true)]

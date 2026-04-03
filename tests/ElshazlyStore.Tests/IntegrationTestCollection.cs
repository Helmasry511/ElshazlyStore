namespace ElshazlyStore.Tests;

/// <summary>
/// Defines a shared xUnit collection so all integration test classes
/// reuse a single <see cref="TestWebApplicationFactory"/> instance.
/// This prevents concurrent Program.cs runs and Serilog freeze conflicts.
/// </summary>
[CollectionDefinition("Integration")]
public sealed class IntegrationTestCollection : ICollectionFixture<TestWebApplicationFactory>
{
}

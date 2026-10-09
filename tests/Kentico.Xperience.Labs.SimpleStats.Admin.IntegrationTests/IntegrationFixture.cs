namespace Kentico.Xperience.Labs.SimpleStats.Admin.IntegrationTests;

/// <summary>
/// Fixture that seeds its rows once (<see cref="CreateSeed"/>) and deletes them at the end.
/// </summary>
[Category(IntegrationDatabase.Category)]
public abstract class IntegrationFixture
{
    // Seeded classes, languages and workspaces need IDs from SeedBuilder.FirstSharedId: a new database has its own rows there.
    protected const int English = SeedBuilder.FirstSharedId + 1;
    protected const int French = SeedBuilder.FirstSharedId + 2;
    protected const int MainWorkspace = SeedBuilder.FirstSharedId + 1;

    [OneTimeSetUp]
    public Task SeedData() => IntegrationDatabase.Seed(CreateSeed());

    [OneTimeTearDown]
    public Task CleanData() => IntegrationDatabase.Clean();

    /// <summary>
    /// Rows of the fixture.
    /// </summary>
    private protected abstract SeedBuilder CreateSeed();
}

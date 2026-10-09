using CMS.Core;
using CMS.DataEngine;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.IntegrationTests;

/// <summary>
/// Points Xperience's <see cref="ConnectionHelper"/> at an empty Xperience database created by <c>kentico-xperience-dbmanager</c>
/// (see README.md). Without <see cref="ConnectionStringVariable"/>, every test is ignored.
/// </summary>
/// <remarks>
/// Each fixture seeds its own rows (<see cref="Seed"/>) and deletes them when it ends (<see cref="Clean"/>), so the database
/// stays empty between fixtures. Cleaning deletes whole tables, so only a database whose name ends with
/// <see cref="DatabaseNameSuffix"/> is accepted.
/// </remarks>
[SetUpFixture]
public sealed class IntegrationDatabase
{
    /// <summary>
    /// Environment variable with the connection string of the test database.
    /// </summary>
    public const string ConnectionStringVariable = "SIMPLESTATS_INTEGRATION_SQL";

    /// <summary>
    /// Required end of the database name, so the tests never clean a real database.
    /// </summary>
    public const string DatabaseNameSuffix = "integration-tests";

    public const string Category = "Integration";

    // Data tables a new database leaves empty: cleaned completely, children first.
    private static readonly string[] dataTables =
    [
        "OM_Activity", "OM_Contact", "CMS_WebPageItem", "CMS_ContentItemTag", "CMS_Tag", "CMS_Taxonomy", "CMS_ContentItemReference",
        "CMS_ContentItemVersion", "CMS_ContentItemCommonData", "CMS_ContentItemLanguageMetadata", "CMS_ContentItem", "CMS_Form",
        "CMS_WebsiteChannel", "CMS_Channel",
    ];

    // Tables a new database already fills: only seeded rows (IDs from SeedBuilder.FirstSharedId) are deleted.
    private static readonly (string Table, string IdColumn)[] sharedTables =
    [
        ("CMS_Class", "ClassID"), ("CMS_ContentFolder", "ContentFolderID"), ("CMS_Workspace", "WorkspaceID"), ("CMS_ContentLanguage", "ContentLanguageID"),
    ];

    private static string? connectionString;

    [OneTimeSetUp]
    public void Initialize()
    {
        string? value = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        if (string.IsNullOrWhiteSpace(value))
        {
            Assert.Ignore($"Integration tests need an empty Xperience database: set {ConnectionStringVariable} (see README.md).");
        }

        string database = new SqlConnectionStringBuilder(value).InitialCatalog;
        if (!database.EndsWith(DatabaseNameSuffix, StringComparison.OrdinalIgnoreCase))
        {
            Assert.Fail($"The database of {ConnectionStringVariable} must end with '{DatabaseNameSuffix}' (its tables are cleaned), not '{database}'.");
        }

        connectionString = value;
        InitializeXperience(value);
    }

    /// <summary>
    /// Deletes earlier seeded rows (for example of a run that was stopped), then inserts the rows of <paramref name="seed"/>.
    /// </summary>
    internal static async Task Seed(SeedBuilder seed)
    {
        await Clean();
        await Execute(seed.Build());
    }

    /// <summary>
    /// Deletes all seeded rows.
    /// </summary>
    internal static Task Clean() =>
        Execute(string.Join(
            "\n",
            dataTables.Select(table => $"DELETE FROM [{table}];")
                .Concat(sharedTables.Select(t => $"DELETE FROM [{t.Table}] WHERE [{t.IdColumn}] >= {SeedBuilder.FirstSharedId};"))));

    private static async Task Execute(string sql)
    {
        await using var connection = new SqlConnection(connectionString ?? throw new InvalidOperationException("The test database is not configured."));
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    // The repositories run their SQL through ConnectionHelper, which needs Xperience's service container and connection string.
    // PreInit registers the services; the application is not initialized (no modules, no license check).
    private static void InitializeXperience(string connection)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:CMSConnectionString"] = connection })
            .Build();

        Service.Use<IConfiguration>(configuration);
        CMSApplication.PreInit(true);
    }
}

using System.Data.Common;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

/// <summary>
/// SQL helpers shared by reports that read several result sets in one batch.
/// Only constant SQL fragments are combined; all values are parameters.
/// </summary>
internal static class StatsSql
{
    /// <summary>
    /// Finds where the query string or fragment of activity alias <c>A</c>'s URL starts (<c>U.[Cut]</c>, 0 when none).
    /// Use with <see cref="ActivityUrlWithoutQuery"/>.
    /// </summary>
    public const string ActivityUrlCutApply = "CROSS APPLY (SELECT PATINDEX(N'%[?#]%', A.[ActivityURL]) AS [Cut]) U";

    /// <summary>
    /// Activity alias <c>A</c>'s URL cut at the first <c>?</c> or <c>#</c>, so query strings (for example UTM tags) and fragments
    /// count toward the same page. Other differences (host, trailing slash, case) are kept as stored. Needs <see cref="ActivityUrlCutApply"/>.
    /// </summary>
    public const string ActivityUrlWithoutQuery = "CASE WHEN U.[Cut] > 0 THEN LEFT(A.[ActivityURL], U.[Cut] - 1) ELSE A.[ActivityURL] END";

    /// <summary>
    /// Returns a check that selects one row (<paramref name="column"/>): <c>0</c> and <c>RETURN</c> when one of the tables
    /// does not exist (for example an optional feature is not installed or unlicensed), else <c>1</c>. Reports must not fail without them.
    /// </summary>
    /// <param name="column">Name of the result column (a constant of the report).</param>
    /// <param name="tables">Table names (constants of the report, never user input).</param>
    public static string BuildAvailabilityCheck(string column, params string[] tables)
    {
        string conditions = string.Join(
            Environment.NewLine + "    OR ",
            tables.Select(table => $"OBJECT_ID(N'[{table}]', N'U') IS NULL"));

        return $"""
            IF {conditions}
            BEGIN
                SELECT CAST(0 AS bit) AS [{column}];
                RETURN;
            END;
            SELECT CAST(1 AS bit) AS [{column}];
            """;
    }

    /// <summary>
    /// Reads the first result set of a batch that starts with <see cref="BuildAvailabilityCheck"/>.
    /// </summary>
    public static async Task<bool> IsAvailable(DbDataReader reader, string column, CancellationToken cancellationToken) =>
        await reader.ReadAsync(cancellationToken) && reader.GetBoolean(reader.GetOrdinal(column));

    /// <summary>
    /// Moves to the next result set, or throws when the batch returned fewer result sets than expected.
    /// </summary>
    /// <param name="reader">Reader of the batch.</param>
    /// <param name="reportName">Report name for the error message, for example "orders and revenue".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task NextResult(DbDataReader reader, string reportName, CancellationToken cancellationToken)
    {
        if (!await reader.NextResultAsync(cancellationToken))
        {
            throw new InvalidOperationException($"The {reportName} query returned fewer result sets than expected.");
        }
    }
}

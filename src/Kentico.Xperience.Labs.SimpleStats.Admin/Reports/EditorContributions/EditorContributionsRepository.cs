using System.Data;
using System.Data.Common;

using CMS.ContentEngine.Internal;
using CMS.DataEngine;
using CMS.Membership;

using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PublishingActivity;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.EditorContributions;

/// <summary>
/// Reads created, last modified and published language variants per administration user from the database.
/// </summary>
internal interface IEditorContributionsRepository
{
    /// <summary>
    /// Returns created per user and day in the range, contributions per user in the range and totals of the range and the previous period.
    /// </summary>
    /// <param name="query">Normalized filter.</param>
    /// <param name="withPublished">Count publishes from content version history.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<EditorContributionsData> GetData(PublishingActivityQuery query, bool withPublished, CancellationToken cancellationToken);
}

internal sealed class EditorContributionsRepository : IEditorContributionsRepository
{
    private const string ReportName = "editor contributions";

    public async Task<EditorContributionsData> GetData(PublishingActivityQuery query, bool withPublished, CancellationToken cancellationToken)
    {
        var range = query.Range;
        var (previousFrom, _) = StatsComparison.GetPreviousRange(range);

        var parameters = new QueryDataParameters
        {
            new DataParameter(StatsContentSql.ClassTypeParameter, ClassType.CONTENT_TYPE),
            new DataParameter(EditorContributionsSql.ServiceUserNameParameter, MembershipConstants.SERVICE_USER_NAME),
            new DataParameter(EditorContributionsSql.PublicUserNameParameter, UserInfoProvider.PublicUserName),
            new DataParameter(EditorContributionsSql.PreviousFromParameter, previousFrom.ToDateTime(TimeOnly.MinValue)),
            new DataParameter(EditorContributionsSql.FromParameter, range.From.ToDateTime(TimeOnly.MinValue)),
            new DataParameter(EditorContributionsSql.ToExclusiveParameter, range.To.AddDays(1).ToDateTime(TimeOnly.MinValue)),
            new DataParameter(EditorContributionsSql.LimitParameter, EditorContributionsReportBuilder.UserLimit),
        };
        if (withPublished)
        {
            parameters.Add(new DataParameter(EditorContributionsSql.PublishActionParameter, (int)ContentItemVersionAction.Publish));
        }
        if (query.Kind is string kind)
        {
            parameters.Add(new DataParameter(StatsContentSql.KindParameter, kind));
        }
        if (range.ChannelId is int channelId)
        {
            parameters.Add(new DataParameter(StatsContentSql.ChannelParameter, channelId));
        }

        string sql = EditorContributionsSql.Build(query.Kind is not null, range.ChannelId is not null, withPublished);

        await using var reader = await ConnectionHelper.ExecuteReaderAsync(sql, parameters, QueryTypeEnum.SQLQuery, CommandBehavior.Default, cancellationToken);

        var daily = await ReadDaily(reader, cancellationToken);
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var (users, userCount) = await ReadUsers(reader, cancellationToken);
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var totals = await ReadTotals(reader, cancellationToken);

        return new(daily, users, userCount, totals);
    }

    private static async Task<IReadOnlyList<EditorDailyRow>> ReadDaily(DbDataReader reader, CancellationToken cancellationToken)
    {
        var user = UserOrdinals.From(reader);
        int dateOrdinal = reader.GetOrdinal(EditorContributionsSql.DateColumn);
        int countOrdinal = reader.GetOrdinal(EditorContributionsSql.CountColumn);

        var rows = new List<EditorDailyRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                user.ReadId(reader),
                user.ReadName(reader),
                user.ReadIsSystem(reader),
                DateOnly.FromDateTime(reader.GetDateTime(dateOrdinal)),
                reader.GetInt32(countOrdinal)));
        }

        return rows;
    }

    private static async Task<(IReadOnlyList<EditorUserRow> Users, int UserCount)> ReadUsers(DbDataReader reader, CancellationToken cancellationToken)
    {
        var user = UserOrdinals.From(reader);
        int createdOrdinal = reader.GetOrdinal("Created");
        int modifiedOrdinal = reader.GetOrdinal("LastModified");
        int publishedOrdinal = reader.GetOrdinal("Published");
        int typesOrdinal = reader.GetOrdinal("ContentTypes");
        int countOrdinal = reader.GetOrdinal("UserCount");

        var rows = new List<EditorUserRow>();
        int userCount = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            userCount = reader.GetInt32(countOrdinal);
            rows.Add(new(
                user.ReadId(reader),
                user.ReadName(reader),
                user.ReadIsSystem(reader),
                reader.GetInt32(createdOrdinal),
                reader.GetInt32(modifiedOrdinal),
                reader.GetInt32(publishedOrdinal),
                reader.GetInt32(typesOrdinal)));
        }

        return (rows, userCount);
    }

    private static async Task<EditorTotalsRow> ReadTotals(DbDataReader reader, CancellationToken cancellationToken)
    {
        if (!await reader.ReadAsync(cancellationToken))
        {
            return EditorTotalsRow.Empty;
        }

        int Get(string column) => reader.GetInt32(reader.GetOrdinal(column));

        return new(
            Get("ActiveEditors"),
            Get("PreviousActiveEditors"),
            Get("Created"),
            Get("PreviousCreated"),
            Get("LastModified"),
            Get("PreviousLastModified"),
            Get("Published"),
            Get("PreviousPublished"));
    }

    /// <summary>
    /// Ordinals of the user columns shared by the result sets.
    /// </summary>
    private sealed record UserOrdinals(int Id, int Name, int IsSystem)
    {
        public static UserOrdinals From(DbDataReader reader) =>
            new(
                reader.GetOrdinal(EditorContributionsSql.UserIdColumn),
                reader.GetOrdinal(EditorContributionsSql.UserNameColumn),
                reader.GetOrdinal(EditorContributionsSql.IsSystemColumn));

        public int? ReadId(DbDataReader reader) => reader.IsDBNull(Id) ? null : reader.GetInt32(Id);

        public string? ReadName(DbDataReader reader) => reader.IsDBNull(Name) ? null : reader.GetString(Name);

        public bool ReadIsSystem(DbDataReader reader) => !reader.IsDBNull(IsSystem) && reader.GetBoolean(IsSystem);
    }
}

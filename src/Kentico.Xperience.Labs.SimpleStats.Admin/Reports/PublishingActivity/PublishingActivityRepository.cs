using System.Data;
using System.Data.Common;

using CMS.ContentEngine;
using CMS.ContentEngine.Internal;
using CMS.DataEngine;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PublishingActivity;

/// <summary>
/// Reads created, first published and updated language variants from the database.
/// </summary>
internal interface IPublishingActivityRepository
{
    /// <summary>
    /// Returns daily counts from the start of the previous period to the end of the range, content types, totals and the slowest to publish.
    /// </summary>
    /// <param name="query">Normalized filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<PublishingActivityData> GetData(PublishingActivityQuery query, CancellationToken cancellationToken);
}

internal sealed class PublishingActivityRepository : IPublishingActivityRepository
{
    private const string ReportName = "publishing activity";

    public async Task<PublishingActivityData> GetData(PublishingActivityQuery query, CancellationToken cancellationToken)
    {
        var range = query.Range;
        var (previousFrom, _) = StatsComparison.GetPreviousRange(range);

        var parameters = new QueryDataParameters
        {
            new DataParameter(StatsContentSql.ClassTypeParameter, ClassType.CONTENT_TYPE),
            new DataParameter(PublishingActivitySql.PublishedStatusParameter, (int)VersionStatus.Published),
            new DataParameter(PublishingActivitySql.UnpublishedStatusParameter, (int)VersionStatus.Unpublished),
            new DataParameter(PublishingActivitySql.PublishActionParameter, (int)ContentItemVersionAction.Publish),
            new DataParameter(PublishingActivitySql.FirstPublishToleranceParameter, PublishingActivityReportBuilder.FirstPublishToleranceSeconds),
            new DataParameter(PublishingActivitySql.PreviousFromParameter, previousFrom.ToDateTime(TimeOnly.MinValue)),
            new DataParameter(PublishingActivitySql.FromParameter, range.From.ToDateTime(TimeOnly.MinValue)),
            new DataParameter(PublishingActivitySql.ToExclusiveParameter, range.To.AddDays(1).ToDateTime(TimeOnly.MinValue)),
            new DataParameter(PublishingActivitySql.LimitParameter, PublishingActivityReportBuilder.ListLimit),
        };
        if (query.Kind is string kind)
        {
            parameters.Add(new DataParameter(StatsContentSql.KindParameter, kind));
        }
        if (range.ChannelId is int channelId)
        {
            parameters.Add(new DataParameter(StatsContentSql.ChannelParameter, channelId));
        }

        string sql = PublishingActivitySql.Build(query.Kind is not null, range.ChannelId is not null);

        await using var reader = await ConnectionHelper.ExecuteReaderAsync(sql, parameters, QueryTypeEnum.SQLQuery, CommandBehavior.Default, cancellationToken);

        var daily = await ReadDaily(reader, cancellationToken);
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var types = await ReadContentTypes(reader, cancellationToken);
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var totals = await ReadTotals(reader, cancellationToken);
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var slowest = await ReadSlowest(reader, cancellationToken);

        return new(daily, types, totals, slowest);
    }

    private static async Task<IReadOnlyList<StatsDailyCount>> ReadDaily(DbDataReader reader, CancellationToken cancellationToken)
    {
        int keyOrdinal = reader.GetOrdinal(PublishingActivitySql.SeriesKeyColumn);
        int dateOrdinal = reader.GetOrdinal(PublishingActivitySql.DateColumn);
        int countOrdinal = reader.GetOrdinal(PublishingActivitySql.CountColumn);

        var rows = new List<StatsDailyCount>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                reader.GetString(keyOrdinal),
                DateOnly.FromDateTime(reader.GetDateTime(dateOrdinal)),
                reader.GetInt32(countOrdinal)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<PublishingActivityTypeRow>> ReadContentTypes(DbDataReader reader, CancellationToken cancellationToken)
    {
        int idOrdinal = reader.GetOrdinal("ClassID");
        int nameOrdinal = reader.GetOrdinal("ClassDisplayName");
        int createdOrdinal = reader.GetOrdinal("Created");
        int publishedOrdinal = reader.GetOrdinal("FirstPublished");
        int updatesOrdinal = reader.GetOrdinal("Updates");
        int medianOrdinal = reader.GetOrdinal("MedianDays");

        var rows = new List<PublishingActivityTypeRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                reader.GetInt32(idOrdinal),
                reader.IsDBNull(nameOrdinal) ? string.Empty : reader.GetString(nameOrdinal),
                reader.GetInt32(createdOrdinal),
                reader.GetInt32(publishedOrdinal),
                reader.GetInt32(updatesOrdinal),
                reader.IsDBNull(medianOrdinal) ? null : reader.GetDouble(medianOrdinal)));
        }

        return rows;
    }

    private static async Task<PublishingActivityTotalsRow> ReadTotals(DbDataReader reader, CancellationToken cancellationToken)
    {
        if (!await reader.ReadAsync(cancellationToken))
        {
            return PublishingActivityTotalsRow.Empty;
        }

        int medianOrdinal = reader.GetOrdinal("MedianDays");
        int percentileOrdinal = reader.GetOrdinal("Percentile90Days");

        return new(
            reader.GetInt32(reader.GetOrdinal("PublishedDateUnknown")),
            reader.IsDBNull(medianOrdinal) ? null : reader.GetDouble(medianOrdinal),
            reader.IsDBNull(percentileOrdinal) ? null : reader.GetDouble(percentileOrdinal));
    }

    private static async Task<IReadOnlyList<PublishingActivitySlowRow>> ReadSlowest(DbDataReader reader, CancellationToken cancellationToken)
    {
        int idOrdinal = reader.GetOrdinal("VariantID");
        int nameOrdinal = reader.GetOrdinal("DisplayName");
        int typeOrdinal = reader.GetOrdinal("ClassDisplayName");
        int languageOrdinal = reader.GetOrdinal("ContentLanguageDisplayName");
        int channelOrdinal = reader.GetOrdinal("ChannelDisplayName");
        int reusableOrdinal = reader.GetOrdinal("IsReusable");
        int workspaceOrdinal = reader.GetOrdinal("WorkspaceDisplayName");
        int createdOrdinal = reader.GetOrdinal("CreatedWhen");
        int publishedOrdinal = reader.GetOrdinal("FirstPublishedWhen");
        var links = StatsContentLinkReader.From(reader, withChannels: true);

        var rows = new List<PublishingActivitySlowRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                reader.GetInt32(idOrdinal),
                reader.GetString(nameOrdinal),
                reader.IsDBNull(typeOrdinal) ? string.Empty : reader.GetString(typeOrdinal),
                reader.GetString(languageOrdinal),
                reader.GetDateTime(createdOrdinal),
                reader.GetDateTime(publishedOrdinal))
            {
                Link = links.Read(reader),
                Channel = reader.IsDBNull(channelOrdinal) ? null : reader.GetString(channelOrdinal),
                IsReusable = !reader.IsDBNull(reusableOrdinal) && reader.GetBoolean(reusableOrdinal),
                Workspace = reader.IsDBNull(workspaceOrdinal) ? null : reader.GetString(workspaceOrdinal),
            });
        }

        return rows;
    }
}

using System.Data;
using System.Data.Common;

using CMS.DataEngine;
using CMS.EmailLibrary;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PublishingCalendar;

/// <summary>
/// Reads scheduled publishes, unpublishes and email sends and recently published variants from the database.
/// </summary>
internal interface IPublishingCalendarRepository
{
    /// <summary>
    /// Returns counts, upcoming events per day and the lists for the query. Content events and sends are read separately,
    /// so lists are not ordered or limited across both (see <see cref="PublishingCalendarData"/>).
    /// </summary>
    /// <param name="query">Normalized filter.</param>
    /// <param name="now">Server time the window and limits are counted from (scheduled times are compared as stored).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<PublishingCalendarData> GetData(PublishingCalendarQuery query, DateTime now, CancellationToken cancellationToken);
}

internal sealed class PublishingCalendarRepository : IPublishingCalendarRepository
{
    private const string ReportName = "publishing calendar";

    public async Task<PublishingCalendarData> GetData(PublishingCalendarQuery query, DateTime now, CancellationToken cancellationToken)
    {
        bool withSends = PublishingCalendarReportBuilder.IncludesSends(query.Filter.Kind);

        var parameters = new QueryDataParameters
        {
            new DataParameter(StatsContentSql.ClassTypeParameter, ClassType.CONTENT_TYPE),
            new DataParameter(PublishingCalendarSql.UpcomingFromParameter, now),
            new DataParameter(PublishingCalendarSql.UpcomingToParameter, PublishingCalendarReportBuilder.GetUpcomingTo(now, query.Window)),
            new DataParameter(PublishingCalendarSql.RecentFromParameter, PublishingCalendarReportBuilder.GetRecentFrom(now)),
            new DataParameter(PublishingCalendarSql.LimitParameter, PublishingCalendarReportBuilder.ListLimit),
        };
        if (query.Filter.Kind is string kind)
        {
            parameters.Add(new DataParameter(StatsContentSql.KindParameter, kind));
        }
        if (query.Filter.ChannelId is int channelId)
        {
            parameters.Add(new DataParameter(StatsContentSql.ChannelParameter, channelId));
        }
        if (withSends)
        {
            parameters.Add(new DataParameter(PublishingCalendarSql.RegularPurposeParameter, EmailPurpose.Regular.ToString()));
        }

        string sql = PublishingCalendarSql.Build(query.Filter.Kind is not null, query.Filter.ChannelId is not null, withSends);

        await using var reader = await ConnectionHelper.ExecuteReaderAsync(sql, parameters, QueryTypeEnum.SQLQuery, CommandBehavior.Default, cancellationToken);

        var counts = await ReadCounts(reader, cancellationToken);
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var days = await ReadDays(reader, cancellationToken);
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var upcoming = await ReadRows(reader, withAction: true, cancellationToken);
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var recent = await ReadRows(reader, withAction: false, cancellationToken);

        var data = new PublishingCalendarData(counts, days, upcoming, recent) { Now = now };

        // Without the email tables (or for kinds without emails), there are no sends.
        if (!withSends
            || !await reader.NextResultAsync(cancellationToken)
            || !await StatsSql.IsAvailable(reader, PublishingCalendarSql.SendsAvailableColumn, cancellationToken))
        {
            return data;
        }

        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var sendCounts = await ReadSendCounts(reader, cancellationToken);
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var sendDays = await ReadSendDays(reader, cancellationToken);
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var sendUpcoming = await ReadRows(reader, withAction: true, cancellationToken);

        return data with
        {
            Counts = counts.Add(sendCounts),
            Days = [.. days, .. sendDays],
            Upcoming = [.. upcoming, .. sendUpcoming],
        };
    }

    private static async Task<PublishingCountsRow> ReadCounts(DbDataReader reader, CancellationToken cancellationToken)
    {
        if (!await reader.ReadAsync(cancellationToken))
        {
            return PublishingCountsRow.Empty;
        }

        return new(
            reader.GetInt32(reader.GetOrdinal("UpcomingPublish")),
            reader.GetInt32(reader.GetOrdinal("UpcomingUnpublish")),
            0,
            reader.GetInt32(reader.GetOrdinal("RecentlyPublished")));
    }

    private static async Task<PublishingCountsRow> ReadSendCounts(DbDataReader reader, CancellationToken cancellationToken)
    {
        if (!await reader.ReadAsync(cancellationToken))
        {
            return PublishingCountsRow.Empty;
        }

        return PublishingCountsRow.Empty with
        {
            UpcomingSend = reader.GetInt32(reader.GetOrdinal("UpcomingSend")),
        };
    }

    private static async Task<IReadOnlyList<PublishingDayRow>> ReadDays(DbDataReader reader, CancellationToken cancellationToken)
    {
        int dayOrdinal = reader.GetOrdinal("Day");
        int publishOrdinal = reader.GetOrdinal("Publish");
        int unpublishOrdinal = reader.GetOrdinal("Unpublish");

        var rows = new List<PublishingDayRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                DateOnly.FromDateTime(reader.GetDateTime(dayOrdinal)),
                reader.GetInt32(publishOrdinal),
                reader.GetInt32(unpublishOrdinal),
                0));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<PublishingDayRow>> ReadSendDays(DbDataReader reader, CancellationToken cancellationToken)
    {
        int dayOrdinal = reader.GetOrdinal("Day");
        int sendOrdinal = reader.GetOrdinal("Send");

        var rows = new List<PublishingDayRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(DateOnly.FromDateTime(reader.GetDateTime(dayOrdinal)), 0, 0, reader.GetInt32(sendOrdinal)));
        }

        return rows;
    }

    /// <summary>
    /// Reads a list. With <paramref name="withAction"/>, also the <c>Action</c> column (events).
    /// </summary>
    private static async Task<IReadOnlyList<PublishingRow>> ReadRows(DbDataReader reader, bool withAction, CancellationToken cancellationToken)
    {
        int idOrdinal = reader.GetOrdinal("RowID");
        int whenOrdinal = reader.GetOrdinal("When");
        int actionOrdinal = withAction ? reader.GetOrdinal("Action") : -1;
        int nameOrdinal = reader.GetOrdinal("DisplayName");
        int typeOrdinal = reader.GetOrdinal("ClassDisplayName");
        int languageOrdinal = reader.GetOrdinal("ContentLanguageDisplayName");
        int channelOrdinal = reader.GetOrdinal("ChannelDisplayName");
        int modifiedByOrdinal = reader.GetOrdinal("ModifiedBy");
        int reusableOrdinal = reader.GetOrdinal("IsReusable");
        int workspaceOrdinal = reader.GetOrdinal("WorkspaceDisplayName");
        var links = StatsContentLinkReader.From(reader, withChannels: true);

        var rows = new List<PublishingRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                reader.GetInt32(idOrdinal),
                reader.GetDateTime(whenOrdinal),
                withAction ? ToAction(reader.GetInt32(actionOrdinal)) : null,
                reader.GetString(nameOrdinal),
                reader.IsDBNull(typeOrdinal) ? string.Empty : reader.GetString(typeOrdinal),
                reader.GetString(languageOrdinal),
                reader.IsDBNull(channelOrdinal) ? null : reader.GetString(channelOrdinal),
                reader.IsDBNull(modifiedByOrdinal) ? null : reader.GetString(modifiedByOrdinal))
            {
                Link = links.Read(reader),
                IsReusable = !reader.IsDBNull(reusableOrdinal) && reader.GetBoolean(reusableOrdinal),
                Workspace = reader.IsDBNull(workspaceOrdinal) ? null : reader.GetString(workspaceOrdinal),
            });
        }

        return rows;
    }

    /// <summary>
    /// <c>Action</c> of <see cref="PublishingCalendarSql"/>: a <see cref="PublishingAction"/> value.
    /// </summary>
    internal static PublishingAction ToAction(int action) =>
        action switch
        {
            (int)PublishingAction.Publish => PublishingAction.Publish,
            (int)PublishingAction.Unpublish => PublishingAction.Unpublish,
            (int)PublishingAction.Send => PublishingAction.Send,
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown scheduled action."),
        };
}

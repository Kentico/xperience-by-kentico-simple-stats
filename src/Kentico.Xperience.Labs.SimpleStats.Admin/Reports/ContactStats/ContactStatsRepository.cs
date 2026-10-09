using System.Data;
using System.Data.Common;

using CMS.Activities;
using CMS.ContentEngine;
using CMS.DataEngine;

using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.TagUsage;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContactStats;

/// <summary>
/// Reads the activities of one contact.
/// </summary>
internal interface IContactStatsRepository
{
    /// <summary>
    /// Returns whether the contact exists, its creation date and its activity types (any date).
    /// </summary>
    public Task<ContactStatsInfo> GetInfo(int contactId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the data of the main batch for the range (and the previous period from <paramref name="previousFrom"/>) and types.
    /// </summary>
    public Task<ContactStatsData> GetData(int contactId, DateOnly previousFrom, DateOnly from, DateOnly to, IReadOnlyList<string> activityTypes, int? taxonomyId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns activity counts by weekday and hour in the range for the types. Only non-zero cells.
    /// </summary>
    public Task<IReadOnlyList<ContactHeatmapCell>> GetHeatmap(int contactId, DateOnly from, DateOnly to, IReadOnlyList<string> activityTypes, CancellationToken cancellationToken);
}

internal sealed class ContactStatsRepository : IContactStatsRepository
{
    private const string ReportName = "contact stats";

    public async Task<ContactStatsInfo> GetInfo(int contactId, CancellationToken cancellationToken)
    {
        var parameters = new QueryDataParameters { new DataParameter(ContactStatsSql.ContactParameter, contactId) };

        await using var reader = await ConnectionHelper.ExecuteReaderAsync(ContactStatsSql.InfoQuery, parameters, QueryTypeEnum.SQLQuery, CommandBehavior.Default, cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return ContactStatsInfo.Missing;
        }

        DateTime? created = reader.IsDBNull(0) ? null : reader.GetDateTime(0);

        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var types = new List<ContactActivityTypeCount>();
        while (await reader.ReadAsync(cancellationToken))
        {
            types.Add(new(reader.GetString(0), reader.GetInt32(1), reader.GetDateTime(2)));
        }

        return new(true, created, types);
    }

    public async Task<ContactStatsData> GetData(int contactId, DateOnly previousFrom, DateOnly from, DateOnly to, IReadOnlyList<string> activityTypes, int? taxonomyId, CancellationToken cancellationToken)
    {
        var parameters = CreateParameters(contactId, from, to, activityTypes);
        parameters.Add(new DataParameter(ContactStatsSql.PreviousFromParameter, previousFrom.ToDateTime(TimeOnly.MinValue)));
        parameters.Add(new DataParameter("@PageVisitType", PredefinedActivityType.PAGE_VISIT));
        parameters.Add(new DataParameter("@LandingPageType", PredefinedActivityType.LANDING_PAGE));
        parameters.Add(new DataParameter("@FormSubmitType", PredefinedActivityType.BIZFORM_SUBMIT));
        parameters.Add(new DataParameter("@EmailClickType", PredefinedActivityType.EMAIL_CLICK));
        parameters.Add(new DataParameter("@PageLimit", ContactStatsReportBuilder.PageLimit));
        parameters.Add(new DataParameter("@FormLimit", ContactStatsReportBuilder.ItemLimit));
        parameters.Add(new DataParameter("@EmailLimit", ContactStatsReportBuilder.ItemLimit));
        parameters.Add(new DataParameter("@SourceLimit", ContactStatsReportBuilder.SourceLimit));
        parameters.Add(new DataParameter("@InterestLimit", ContactStatsReportBuilder.InterestLimit));
        parameters.Add(new DataParameter("@TagLimit", ContactStatsReportBuilder.InterestLimit));
        parameters.Add(new DataParameter(ContactStatsSql.TaxonomyParameter, taxonomyId ?? 0));
        parameters.Add(new DataParameter(ContactStatsSql.PublishedStatusParameter, (int)VersionStatus.Published));

        await using var reader = await ConnectionHelper.ExecuteReaderAsync(ContactStatsSql.Batch, parameters, QueryTypeEnum.SQLQuery, CommandBehavior.Default, cancellationToken);

        // 1. Totals.
        var totals = ContactStatsTotalsRow.Empty;
        if (await reader.ReadAsync(cancellationToken))
        {
            totals = new(
                Int(reader, "Activities"),
                Int(reader, "PreviousActivities"),
                Int(reader, "Sessions"),
                Int(reader, "PageVisits"),
                Int(reader, "FormSubmissions"),
                Int(reader, "EmailClicks"),
                Int(reader, "ActiveDays"),
                Int(reader, "CampaignSessions"),
                Date(reader, "FirstSeen"),
                Date(reader, "LastSeen"));
        }

        // 2. Daily.
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var daily = new List<StatsDailyCount>();
        while (await reader.ReadAsync(cancellationToken))
        {
            daily.Add(new(reader.GetString(0), DateOnly.FromDateTime(reader.GetDateTime(1)), reader.GetInt32(2)));
        }

        // 3. Pages.
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var pages = new List<ContactStatsPageRow>();
        int pageCount = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            pages.Add(new(
                reader.IsDBNull(reader.GetOrdinal("PageGUID")) ? null : reader.GetGuid(reader.GetOrdinal("PageGUID")),
                NullableInt(reader, "LanguageID"),
                Int(reader, "Visits"),
                reader.GetDateTime(reader.GetOrdinal("LastVisited")),
                Text(reader, "Url"),
                Text(reader, "DisplayName"),
                Text(reader, "Language"),
                Text(reader, "Channel")));
            pageCount = Int(reader, "PageCount");
        }

        // 4. Forms.
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var (forms, formCount) = await ReadItems(reader, withEmailColumns: false, cancellationToken);

        // 5. Emails (the INSERT and UPDATE return no result set).
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var (emails, emailCount) = await ReadItems(reader, withEmailColumns: true, cancellationToken);

        // 6. Sources, 7. source and content pairs.
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var (sources, sourceCount) = await ReadSources(reader, withContent: false, cancellationToken);
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var (contents, contentCount) = await ReadSources(reader, withContent: true, cancellationToken);

        // 8. Content types.
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var interests = new List<ContactStatsInterestRow>();
        int interestCount = 0;
        int interestVisits = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            interests.Add(new(Int(reader, "ClassID"), Text(reader, "ClassDisplayName") ?? string.Empty, Int(reader, "Visits"), Int(reader, "Pages")));
            interestCount = Int(reader, "GroupCount");
            interestVisits = Int(reader, "MatchedVisits");
        }

        // 9. Taxonomy options (the INSERT into @VisitTags returns no result set).
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var taxonomies = new List<TagUsageTaxonomyOption>();
        while (await reader.ReadAsync(cancellationToken))
        {
            taxonomies.Add(new(Int(reader, "TaxonomyID"), Text(reader, "TaxonomyTitle") ?? string.Empty, Int(reader, "Tags")));
        }

        // 10. Tags.
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var tags = new List<ContactStatsTagRow>();
        int tagCount = 0;
        int tagVisits = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            tags.Add(new(Int(reader, "TagID"), Text(reader, "TagTitle") ?? string.Empty, Text(reader, "TaxonomyTitle") ?? string.Empty, Int(reader, "Visits"), Int(reader, "Pages")));
            tagCount = Int(reader, "GroupCount");
            tagVisits = Int(reader, "TagVisits");
        }

        return new(totals, daily, pages, pageCount, forms, formCount, emails, emailCount, sources, sourceCount, contents, contentCount, interests, interestCount, interestVisits)
        {
            Tags = tags,
            TagCount = tagCount,
            TagVisits = tagVisits,
            TaxonomyOptions = taxonomies,
        };
    }

    public async Task<IReadOnlyList<ContactHeatmapCell>> GetHeatmap(int contactId, DateOnly from, DateOnly to, IReadOnlyList<string> activityTypes, CancellationToken cancellationToken)
    {
        var parameters = CreateParameters(contactId, from, to, activityTypes);

        await using var reader = await ConnectionHelper.ExecuteReaderAsync(ContactStatsSql.HeatmapQuery, parameters, QueryTypeEnum.SQLQuery, CommandBehavior.Default, cancellationToken);

        var cells = new List<ContactHeatmapCell>();
        while (await reader.ReadAsync(cancellationToken))
        {
            cells.Add(new(Int(reader, "Weekday"), Int(reader, "Hour"), Int(reader, "Activities")));
        }

        return cells;
    }

    /// <summary>
    /// Contact, range and type filter parameters.
    /// </summary>
    internal static QueryDataParameters CreateParameters(int contactId, DateOnly from, DateOnly to, IReadOnlyList<string> activityTypes)
    {
        string types = StatsSql.FormatDelimitedList(activityTypes);

        return
        [
            new DataParameter(ContactStatsSql.ContactParameter, contactId),
            new DataParameter(ContactStatsSql.FromParameter, from.ToDateTime(TimeOnly.MinValue)),
            new DataParameter(ContactStatsSql.ToExclusiveParameter, to.AddDays(1).ToDateTime(TimeOnly.MinValue)),
            new DataParameter(ContactStatsSql.AllTypesParameter, types.Length == 0),
            new DataParameter(ContactStatsSql.TypesParameter, types),
        ];
    }

    private static async Task<(List<ContactStatsItemRow> Rows, int Count)> ReadItems(DbDataReader reader, bool withEmailColumns, CancellationToken cancellationToken)
    {
        var rows = new List<ContactStatsItemRow>();
        int count = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new ContactStatsItemRow(
                NullableInt(reader, "ItemID"),
                Int(reader, "Activities"),
                reader.GetDateTime(reader.GetOrdinal("Last")),
                Text(reader, "DisplayName"));
            if (withEmailColumns)
            {
                row = row with { EmailChannelId = NullableInt(reader, "EmailChannelID"), LanguageName = Text(reader, "LanguageName") };
            }

            rows.Add(row);
            count = Int(reader, "ItemCount");
        }

        return (rows, count);
    }

    private static async Task<(List<ContactStatsSourceRow> Rows, int Count)> ReadSources(DbDataReader reader, bool withContent, CancellationToken cancellationToken)
    {
        var rows = new List<ContactStatsSourceRow>();
        int count = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(Text(reader, "Source") ?? string.Empty, withContent ? Text(reader, "Content") : null, Int(reader, "Sessions")));
            count = Int(reader, "GroupCount");
        }

        return (rows, count);
    }

    private static int Int(DbDataReader reader, string column) => NullableInt(reader, column) ?? 0;

    private static int? NullableInt(DbDataReader reader, string column)
    {
        int ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : Convert.ToInt32(reader.GetValue(ordinal));
    }

    private static string? Text(DbDataReader reader, string column)
    {
        int ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static DateTime? Date(DbDataReader reader, string column)
    {
        int ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }
}

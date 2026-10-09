using System.Globalization;

using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ActivityCounts;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.TopPages;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContactStats;

/// <summary>
/// Kinds of insight lines (stable, for tests and the client).
/// </summary>
public static class ContactStatsInsights
{
    public const string Active = "active";
    public const string NotSeen = "not-seen";
    public const string Trend = "trend";
    public const string MostVisited = "most-visited";
    public const string TopInterest = "top-interest";
    public const string CampaignSource = "campaign-source";
    public const string LatestForm = "latest-form";

    /// <summary>
    /// A contact whose newest activity is older than this many days gets "Not seen for N days" instead of "Active on ...".
    /// </summary>
    public const int InactiveDays = 30;
}

/// <summary>
/// Turns the contact batch into the report, and the heatmap counts into 7 × 24 cells.
/// </summary>
internal static class ContactStatsReportBuilder
{
    public const int PageLimit = 25;
    public const int ItemLimit = 25;
    public const int SourceLimit = 25;
    public const int InterestLimit = 25;

    public const string UnknownPageLabel = "(unknown page)";
    public const string DeletedFormLabel = "(deleted form)";
    public const string DeletedEmailLabel = "(deleted email)";

    /// <summary>
    /// Fills everything except <see cref="ContactStatsResult.UpdatedAt"/>.
    /// </summary>
    /// <param name="contactId">Contact ID.</param>
    /// <param name="query">Normalized filter.</param>
    /// <param name="info">Contact info (type options).</param>
    /// <param name="data">Batch data.</param>
    /// <param name="displayNames">Activity type display names.</param>
    /// <param name="hasAnyUtmData">Any activity on the site has a UTM source.</param>
    /// <param name="today">Server date.</param>
    /// <param name="formPath">Admin path of a form's submissions, or <c>null</c>.</param>
    /// <param name="emailPath">Admin path of an email's statistics (email ID, email channel ID, language), or <c>null</c>.</param>
    public static ContactStatsResult Build(
        int contactId,
        ContactStatsQuery query,
        ContactStatsInfo info,
        ContactStatsData data,
        IReadOnlyDictionary<string, string> displayNames,
        bool hasAnyUtmData,
        DateOnly today,
        Func<int, string?> formPath,
        Func<int, int?, string?, string?> emailPath)
    {
        var range = query.Range;
        var t = data.Totals;

        var series = StatsTimeSeriesBuilder.BuildByTotal(
            range,
            data.Daily,
            type => ActivityCountsReportBuilder.GetDisplayName(type, displayNames));

        int rangeDays = range.To.DayNumber - range.From.DayNumber + 1;
        int? daysSinceLastSeen = t.LastSeen is DateTime last ? Math.Max(today.DayNumber - DateOnly.FromDateTime(last).DayNumber, 0) : null;
        var totals = new ContactStatsTotals(
            Math.Max(t.Activities, 0),
            Math.Max(t.Sessions, 0),
            Math.Max(t.PageVisits, 0),
            Math.Max(t.FormSubmissions, 0),
            Math.Max(t.EmailClicks, 0),
            Math.Max(t.ActiveDays, 0),
            rangeDays,
            t.FirstSeen,
            t.LastSeen,
            daysSinceLastSeen);

        var comparison = query.AllTime ? null : StatsComparison.Create(range, totals.Activities, Math.Max(t.PreviousActivities, 0));

        var pages = StatsRankedBuilder.Build(range, data.Pages.Select(ToPageEntry), totals.PageVisits, data.PageCount, PageLimit);

        var forms = StatsRankedBuilder.Build(
            range,
            data.Forms.Select(row => ToItemEntry(row, DeletedFormLabel) with
            {
                AdminPath = row.ItemId is int id && row.DisplayName is not null ? formPath(id) : null,
            }),
            totals.FormSubmissions,
            data.FormCount,
            ItemLimit);

        var emails = StatsRankedBuilder.Build(
            range,
            data.Emails.Select(row => ToItemEntry(row, DeletedEmailLabel) with
            {
                AdminPath = row.ItemId is int id && row.DisplayName is not null ? emailPath(id, row.EmailChannelId, row.LanguageName) : null,
            }),
            totals.EmailClicks,
            data.EmailCount,
            ItemLimit);

        var campaigns = StatsRankedBuilder.Build(
            range,
            data.SourceContents.Select(row => new StatsRankedEntry(
                StatsUtm.GetPairKey(row.Source, row.Content),
                row.Source,
                row.Content ?? StatsUtm.NoValueLabel,
                row.Sessions,
                null,
                null)),
            Math.Max(t.CampaignSessions, 0),
            data.SourceContentCount,
            SourceLimit);

        var interests = StatsRankedBuilder.Build(
            range,
            data.Interests.Select(row => new StatsRankedEntry(
                row.ClassId.ToString(CultureInfo.InvariantCulture),
                row.DisplayName,
                null,
                row.Visits,
                row.Pages,
                null)),
            data.InterestVisits,
            data.InterestCount,
            InterestLimit);

        // A visit counts once per tag, so the values do not add up; the total is the visits that reached any tag.
        var interestTags = StatsRankedBuilder.Build(
            range,
            data.Tags.Select(row => new StatsRankedEntry(
                row.TagId.ToString(CultureInfo.InvariantCulture),
                row.Title,
                row.Taxonomy,
                row.Visits,
                row.Pages,
                null)),
            data.TagVisits,
            data.TagCount,
            InterestLimit);

        bool utm = hasAnyUtmData || t.CampaignSessions > 0;

        var typeOptions = info.Types
            .Select(type => new ContactStatsTypeOption(type.ActivityType, ActivityCountsReportBuilder.GetDisplayName(type.ActivityType, displayNames), type.Count))
            .ToList();

        return new(
            contactId,
            range.From,
            range.To,
            range.Grouping,
            query.AllTime,
            query.ActivityTypes,
            typeOptions,
            totals,
            comparison,
            series,
            pages,
            forms,
            emails,
            campaigns,
            interests,
            interestTags,
            query.TaxonomyId,
            data.TaxonomyOptions,
            utm,
            BuildInsights(totals, comparison, pages, interests, utm ? data.Sources : [], data.Forms, interestTags),
            null,
            null);
    }

    /// <summary>
    /// Short insight lines that apply, in display order.
    /// </summary>
    internal static List<ContactStatsInsight> BuildInsights(
        ContactStatsTotals totals,
        StatsComparison? comparison,
        StatsRankedResult pages,
        StatsRankedResult interests,
        IReadOnlyList<ContactStatsSourceRow> sources,
        IReadOnlyList<ContactStatsItemRow> forms,
        StatsRankedResult? tags = null)
    {
        var lines = new List<ContactStatsInsight>();

        if (totals.DaysSinceLastSeen is int days && days > ContactStatsInsights.InactiveDays)
        {
            lines.Add(new(ContactStatsInsights.NotSeen, Format($"Not seen for {days} days")));
        }
        else if (totals.ActiveDays > 0)
        {
            lines.Add(new(ContactStatsInsights.Active, Format($"Active on {totals.ActiveDays} of the last {totals.RangeDays} days")));
        }

        if (comparison is { Change: double change } && change != 0)
        {
            int percent = (int)Math.Round(Math.Abs(change) * 100, MidpointRounding.AwayFromZero);
            int previousDays = comparison.PreviousTo.DayNumber - comparison.PreviousFrom.DayNumber + 1;
            lines.Add(new(
                ContactStatsInsights.Trend,
                Format($"Activity {(change > 0 ? "up" : "down")} {percent}% vs previous {previousDays} days")));
        }

        if (pages.Items.Count > 0)
        {
            lines.Add(new(ContactStatsInsights.MostVisited, $"Most visited: {pages.Items[0].Label}"));
        }

        // Tags are more specific than content types, so they win when the visits reached any.
        if (tags is { Items.Count: > 0 })
        {
            var tag = tags.Items[0];
            lines.Add(new(ContactStatsInsights.TopInterest, $"Top interest: {tag.Label} ({tag.SecondaryLabel})"));
        }
        else if (interests.Items.Count > 0)
        {
            lines.Add(new(ContactStatsInsights.TopInterest, $"Top interest: {interests.Items[0].Label}"));
        }

        var topSource = sources.Where(s => s.Sessions > 0).OrderByDescending(s => s.Sessions).ThenBy(s => s.Source, StringComparer.Ordinal).FirstOrDefault();
        if (topSource is not null && totals.Sessions > 0)
        {
            lines.Add(new(
                ContactStatsInsights.CampaignSource,
                Format($"Came from {topSource.Source} in {topSource.Sessions} of {totals.Sessions} sessions")));
        }

        var latestForm = forms.OrderByDescending(f => f.Last).FirstOrDefault();
        if (latestForm is not null)
        {
            string name = latestForm.DisplayName ?? DeletedFormLabel;
            lines.Add(new(
                ContactStatsInsights.LatestForm,
                Format($"Submitted {name} {latestForm.Count} {(latestForm.Count == 1 ? "time" : "times")}")));
        }

        return lines;
    }

    /// <summary>
    /// All 7 × 24 cells (Monday first), zero-filled.
    /// </summary>
    public static ContactHeatmapResult BuildHeatmap(int contactId, ContactStatsQuery query, IReadOnlyList<ContactHeatmapCell> counts)
    {
        var lookup = counts
            .Where(c => c.Weekday is >= 0 and < 7 && c.Hour is >= 0 and < 24)
            .GroupBy(c => (c.Weekday, c.Hour))
            .ToDictionary(g => g.Key, g => g.Sum(c => Math.Max(c.Count, 0)));

        var cells = new List<ContactHeatmapCell>(7 * 24);
        for (int weekday = 0; weekday < 7; weekday++)
        {
            for (int hour = 0; hour < 24; hour++)
            {
                cells.Add(new(weekday, hour, lookup.GetValueOrDefault((weekday, hour))));
            }
        }

        return new(
            contactId,
            query.Range.From,
            query.Range.To,
            query.ActivityTypes,
            cells,
            cells.Max(c => c.Count),
            cells.Sum(c => c.Count));
    }

    private static StatsRankedEntry ToPageEntry(ContactStatsPageRow row)
    {
        string? url = string.IsNullOrWhiteSpace(row.Url) ? null : row.Url;
        string key = row.PageGuid is Guid guid
            ? string.Create(CultureInfo.InvariantCulture, $"{guid:N}-{row.LanguageId ?? 0}")
            : "url:" + (url ?? string.Empty);
        string label = !string.IsNullOrWhiteSpace(row.DisplayName) ? row.DisplayName : url ?? UnknownPageLabel;
        string details = string.Join(" · ", new[] { row.Channel, row.Language }.Where(value => !string.IsNullOrWhiteSpace(value)));

        // Same link as the top pages report: the public URL, opened in a new tab.
        return new(key, label, details.Length > 0 ? details : null, row.Visits, null, TopPagesReportBuilder.GetPublicUrl(url));
    }

    private static StatsRankedEntry ToItemEntry(ContactStatsItemRow row, string deletedLabel) =>
        new(
            (row.ItemId ?? 0).ToString(CultureInfo.InvariantCulture),
            string.IsNullOrWhiteSpace(row.DisplayName) ? deletedLabel : row.DisplayName,
            // Last activity (server time) as a sortable text.
            row.Last.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            row.Count,
            null,
            null);

    private static string Format(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}

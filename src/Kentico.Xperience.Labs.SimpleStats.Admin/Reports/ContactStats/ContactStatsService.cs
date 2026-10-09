using System.Globalization;

using CMS.Helpers;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.DigitalMarketing.UIPages;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ActivityCounts;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.EmailSummary;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.FormSubmissions;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContactStats;

/// <summary>
/// Builds the contact "Stats (Labs)" tab.
/// </summary>
public interface IContactStatsService
{
    /// <summary>
    /// Returns whether the contact exists (cached with the contact's activity types).
    /// </summary>
    public Task<bool> ContactExists(int contactId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the report of the contact for the filter.
    /// </summary>
    /// <param name="contactId">Contact ID.</param>
    /// <param name="filter">Filter from the client, <c>null</c> for defaults (All time, all types).</param>
    /// <param name="today">Server date.</param>
    /// <param name="refresh">When <c>true</c>, cached data is dropped and read again from the database.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<ContactStatsResult> GetReport(int contactId, ContactStatsFilter? filter, DateOnly today, bool refresh, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the weekday × hour heatmap of the contact for the filter (same range and types as <see cref="GetReport"/>). Own query and cache item.
    /// </summary>
    public Task<ContactHeatmapResult> GetHeatmap(int contactId, ContactStatsFilter? filter, DateOnly today, bool refresh, CancellationToken cancellationToken);
}

internal sealed class ContactStatsService(
    IContactStatsRepository repository,
    IActivityCountsRepository activityCountsRepository,
    IStatsUtmDataRepository utmDataRepository,
    IStatsAdminLinks adminLinks,
    IProgressiveCache cache,
    IStatsCacheInvalidator cacheInvalidator,
    TimeProvider clock) : IContactStatsService
{
    private readonly IContactStatsRepository repository = repository;
    private readonly IActivityCountsRepository activityCountsRepository = activityCountsRepository;
    private readonly IStatsUtmDataRepository utmDataRepository = utmDataRepository;
    private readonly IStatsAdminLinks adminLinks = adminLinks;
    private readonly IProgressiveCache cache = cache;
    private readonly IStatsCacheInvalidator cacheInvalidator = cacheInvalidator;
    private readonly TimeProvider clock = clock;

    public async Task<bool> ContactExists(int contactId, CancellationToken cancellationToken) =>
        contactId > 0 && (await GetInfo(contactId, refresh: false, cancellationToken)).Exists;

    public async Task<ContactStatsResult> GetReport(int contactId, ContactStatsFilter? filter, DateOnly today, bool refresh, CancellationToken cancellationToken)
    {
        var info = await GetInfo(contactId, refresh, cancellationToken);
        var query = (filter ?? new ContactStatsFilter()).Normalize(today, info);
        var range = query.Range;

        // All time has no previous period: the batch then reads the range only.
        var previousFrom = query.AllTime ? range.From : StatsComparison.GetPreviousRange(range).From;

        // Grouping is not part of the key: the daily aggregate is cached.
        var settings = StatsCache.CreateSettings(
            "contact-stats",
            contactId,
            previousFrom.DayNumber,
            range.From.DayNumber,
            range.To.DayNumber,
            TypesKey(query.ActivityTypes),
            query.TaxonomyId ?? 0);

        var snapshot = await cache.LoadAsync(
            cacheInvalidator,
            settings,
            refresh,
            async token => new ContactStatsSnapshot(
                info.Exists
                    ? await repository.GetData(contactId, previousFrom, range.From, range.To, query.ActivityTypes, query.TaxonomyId, token)
                    : ContactStatsData.Empty,
                clock.GetUtcNow()),
            cancellationToken);

        var displayNames = await cache.LoadAsync(
            cacheInvalidator,
            StatsCache.CreateSettings("activity-type-names"),
            refresh,
            activityCountsRepository.GetActivityTypeDisplayNames,
            cancellationToken);

        bool hasAnyUtmData = snapshot.Data.Totals.CampaignSessions > 0
            || await StatsUtm.HasAnyUtmData(cache, cacheInvalidator, utmDataRepository, refresh, cancellationToken);

        var result = ContactStatsReportBuilder.Build(
            contactId,
            query,
            info,
            snapshot.Data,
            displayNames,
            hasAnyUtmData,
            today,
            formId => FormSubmissionsService.GetSubmissionsPath(adminLinks, formId),
            (emailId, channelId, language) => EmailSummaryService.GetStatisticsPath(adminLinks, emailId, channelId, language));

        var contactParameters = new PageParameterValues { { typeof(ContactEditSection), contactId } };

        return result with
        {
            Series = result.Series with { UpdatedAt = snapshot.ReadAt },
            TopPages = result.TopPages with { UpdatedAt = snapshot.ReadAt },
            Forms = result.Forms with { UpdatedAt = snapshot.ReadAt },
            Emails = result.Emails with { UpdatedAt = snapshot.ReadAt },
            Campaigns = result.Campaigns with { UpdatedAt = snapshot.ReadAt },
            Interests = result.Interests with { UpdatedAt = snapshot.ReadAt },
            InterestTags = result.InterestTags with { UpdatedAt = snapshot.ReadAt },
            ActivitiesPath = adminLinks.GetPath<ContactActivityList>(contactParameters),
            PagePath = adminLinks.GetPath<UIPages.ContactStatsPage>(contactParameters),
            UpdatedAt = snapshot.ReadAt,
        };
    }

    public async Task<ContactHeatmapResult> GetHeatmap(int contactId, ContactStatsFilter? filter, DateOnly today, bool refresh, CancellationToken cancellationToken)
    {
        var info = await GetInfo(contactId, refresh, cancellationToken);
        var query = (filter ?? new ContactStatsFilter()).Normalize(today, info);

        var settings = StatsCache.CreateSettings(
            "contact-stats-heatmap",
            contactId,
            query.Range.From.DayNumber,
            query.Range.To.DayNumber,
            TypesKey(query.ActivityTypes));

        var snapshot = await cache.LoadAsync(
            cacheInvalidator,
            settings,
            refresh,
            async token => new ContactHeatmapSnapshot(
                info.Exists ? await repository.GetHeatmap(contactId, query.Range.From, query.Range.To, query.ActivityTypes, token) : [],
                clock.GetUtcNow()),
            cancellationToken);

        return ContactStatsReportBuilder.BuildHeatmap(contactId, query, snapshot.Cells) with { UpdatedAt = snapshot.ReadAt };
    }

    private Task<ContactStatsInfo> GetInfo(int contactId, bool refresh, CancellationToken cancellationToken) =>
        cache.LoadAsync(
            cacheInvalidator,
            StatsCache.CreateSettings("contact-stats-info", contactId),
            refresh,
            token => repository.GetInfo(contactId, token),
            cancellationToken);

    /// <summary>
    /// Cache key part of the (already sorted) types: length-prefixed, so no value can make two filters share a key.
    /// </summary>
    internal static string TypesKey(IReadOnlyList<string> types) =>
        types.Count == 0
            ? "all"
            : string.Join(',', types.Select(type => string.Create(CultureInfo.InvariantCulture, $"{type.Length}:{type.ToLowerInvariant()}")));
}

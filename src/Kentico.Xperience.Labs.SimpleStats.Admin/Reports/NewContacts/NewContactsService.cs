using CMS.Helpers;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.NewContacts;

/// <summary>
/// Builds the new contacts report.
/// </summary>
public interface INewContactsService
{
    /// <summary>
    /// Returns the report for the query. The channel is ignored: contacts have no channel.
    /// </summary>
    /// <param name="query">Normalized filter.</param>
    /// <param name="refresh">When <c>true</c>, cached data is dropped and read again from the database (then cached again).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<NewContactsResult> GetReport(StatsQuery query, bool refresh, CancellationToken cancellationToken);
}

internal sealed class NewContactsService(
    INewContactsRepository repository,
    IProgressiveCache cache,
    IStatsCacheInvalidator cacheInvalidator,
    TimeProvider clock) : INewContactsService
{
    private readonly INewContactsRepository repository = repository;
    private readonly IProgressiveCache cache = cache;
    private readonly IStatsCacheInvalidator cacheInvalidator = cacheInvalidator;
    private readonly TimeProvider clock = clock;

    public async Task<NewContactsResult> GetReport(StatsQuery query, bool refresh, CancellationToken cancellationToken)
    {
        // Contacts have no channel, so the channel is dropped and does not split the cache key.
        var contactsQuery = query with { ChannelId = null };

        // One query reads the previous period and the range; the builder splits the rows by date.
        var (previousFrom, _) = StatsComparison.GetPreviousRange(contactsQuery);

        // Cache the daily aggregate (not the bucketed result) so switching grouping does not hit the database.
        // The previous period is derived from the range, so the range alone is the key.
        var settings = StatsCache.CreateSettings(
            "new-contacts",
            contactsQuery.From.DayNumber,
            contactsQuery.To.DayNumber);

        var snapshot = await cache.LoadAsync(
            cacheInvalidator,
            settings,
            refresh,
            async token => new NewContactsSnapshot(
                await repository.GetDailyCounts(previousFrom, contactsQuery.To, token),
                clock.GetUtcNow()),
            cancellationToken);

        var result = NewContactsReportBuilder.Build(contactsQuery, snapshot.Rows);

        return result with { Trend = result.Trend with { UpdatedAt = snapshot.ReadAt } };
    }
}

using System.Globalization;

using CMS.Core;
using CMS.Helpers;

using Kentico.Xperience.Admin.Base.UIPages;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.EventLog;

/// <summary>
/// Builds the event log report.
/// </summary>
public interface IEventLogReportService
{
    /// <summary>
    /// Returns the report for the query.
    /// </summary>
    /// <param name="query">Normalized filter (see <see cref="EventLogFilter.Normalize"/>).</param>
    /// <param name="refresh">When <c>true</c>, cached data is dropped and read again from the database (then cached again).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<EventLogResult> GetReport(EventLogQuery query, bool refresh, CancellationToken cancellationToken);
}

internal sealed class EventLogReportService(
    IEventLogRepository repository,
    ISettingsService settingsService,
    IProgressiveCache cache,
    IStatsCacheInvalidator cacheInvalidator,
    IStatsAdminLinks adminLinks,
    TimeProvider clock) : IEventLogReportService
{
    /// <summary>
    /// Settings key of the "Event log size" setting (Settings → System → Event log).
    /// </summary>
    public const string LogSizeSettingsKey = "CMSLogSize";

    private readonly IEventLogRepository repository = repository;
    private readonly ISettingsService settingsService = settingsService;
    private readonly IProgressiveCache cache = cache;
    private readonly IStatsCacheInvalidator cacheInvalidator = cacheInvalidator;
    private readonly IStatsAdminLinks adminLinks = adminLinks;
    private readonly TimeProvider clock = clock;

    public async Task<EventLogResult> GetReport(EventLogQuery query, bool refresh, CancellationToken cancellationToken)
    {
        // Events have no channel, so the channel is dropped and does not split the cache key.
        var normalized = query with { Range = query.Range with { ChannelId = null } };
        var range = normalized.Range;

        // One batch reads the previous period and the range; the builder splits the rows by date.
        var (previousFrom, _) = StatsComparison.GetPreviousRange(range);

        // Cache the daily aggregate (not the bucketed result) so switching grouping does not hit the database.
        var settings = StatsCache.CreateSettings(
            "event-log",
            range.From.DayNumber,
            range.To.DayNumber,
            normalized.EventType ?? "all");

        var snapshot = await cache.LoadAsync(
            cacheInvalidator,
            settings,
            refresh,
            async token => new EventLogReportSnapshot(
                await repository.GetData(previousFrom, range.From, range.To, normalized.EventType, EventLogReportBuilder.TopLimit, token),
                GetLogSizeLimit(),
                clock.GetUtcNow()),
            cancellationToken);

        var result = EventLogReportBuilder.Build(normalized, snapshot.Data, snapshot.LogSizeLimit, GetUserPath);

        return result with
        {
            Trend = result.Trend with { UpdatedAt = snapshot.ReadAt },
            TopSources = result.TopSources with { UpdatedAt = snapshot.ReadAt },
            TopXperienceSources = result.TopXperienceSources with { UpdatedAt = snapshot.ReadAt },
            TopCustomSources = result.TopCustomSources with { UpdatedAt = snapshot.ReadAt },
            TopCodes = result.TopCodes with { UpdatedAt = snapshot.ReadAt },
            TopUsers = result.TopUsers with { UpdatedAt = snapshot.ReadAt },
            EventLogPath = adminLinks.GetPath<EventLogList>(),
            UpdatedAt = snapshot.ReadAt,
        };
    }

    /// <summary>
    /// Maximum number of events kept (0 = events are not logged). <c>null</c> when the setting has no valid value.
    /// </summary>
    private int? GetLogSizeLimit() =>
        int.TryParse(settingsService[LogSizeSettingsKey], NumberStyles.Integer, CultureInfo.InvariantCulture, out int size) && size >= 0
            ? size
            : null;

    /// <inheritdoc cref="StatsUserPaths.GetPath"/>
    private string? GetUserPath(int userId) => StatsUserPaths.GetPath(adminLinks, userId);
}

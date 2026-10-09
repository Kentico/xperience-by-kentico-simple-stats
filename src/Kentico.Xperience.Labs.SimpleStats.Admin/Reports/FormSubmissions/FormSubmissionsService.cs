using CMS.Helpers;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.DigitalMarketing.UIPages;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.FormSubmissions;

/// <summary>
/// Builds the form submissions report.
/// </summary>
public interface IFormSubmissionsService
{
    /// <summary>
    /// Returns the report for the query. The channel is ignored: form data has no channel.
    /// </summary>
    /// <param name="query">Normalized filter.</param>
    /// <param name="refresh">When <c>true</c>, cached data is dropped and read again from the database (then cached again).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<FormSubmissionsResult> GetReport(StatsQuery query, bool refresh, CancellationToken cancellationToken);
}

internal sealed class FormSubmissionsService(
    IFormSubmissionsRepository repository,
    IProgressiveCache cache,
    IStatsCacheInvalidator cacheInvalidator,
    IStatsAdminLinks adminLinks,
    TimeProvider clock) : IFormSubmissionsService
{
    private readonly IFormSubmissionsRepository repository = repository;
    private readonly IProgressiveCache cache = cache;
    private readonly IStatsCacheInvalidator cacheInvalidator = cacheInvalidator;
    private readonly IStatsAdminLinks adminLinks = adminLinks;
    private readonly TimeProvider clock = clock;

    public async Task<FormSubmissionsResult> GetReport(StatsQuery query, bool refresh, CancellationToken cancellationToken)
    {
        // Form data has no channel, so the channel is dropped and does not split the cache key.
        var formsQuery = query with { ChannelId = null };

        // One query reads the previous period and the range; the builder splits the rows by date.
        var (previousFrom, _) = StatsComparison.GetPreviousRange(formsQuery);

        // Cache the daily aggregate (not the bucketed result) so switching grouping does not hit the database.
        // The previous period is derived from the range, so the range alone is the key.
        var settings = StatsCache.CreateSettings(
            "form-submissions",
            formsQuery.From.DayNumber,
            formsQuery.To.DayNumber);

        var snapshot = await cache.LoadAsync(
            cacheInvalidator,
            settings,
            refresh,
            async token => new FormSubmissionsSnapshot(
                await repository.GetData(previousFrom, formsQuery.To, token),
                clock.GetUtcNow()),
            cancellationToken);

        var result = FormSubmissionsReportBuilder.Build(formsQuery, snapshot.Data, GetSubmissionsPath);

        return result with
        {
            Trend = result.Trend with { UpdatedAt = snapshot.ReadAt },
            Forms = result.Forms with { UpdatedAt = snapshot.ReadAt },
            UpdatedAt = snapshot.ReadAt,
        };
    }

    /// <summary>
    /// Path of the "Submissions" tab of the form in the Forms application.
    /// </summary>
    private string? GetSubmissionsPath(int formId) => GetSubmissionsPath(adminLinks, formId);

    /// <summary>
    /// Path of the "Submissions" tab of the form in the Forms application, or <c>null</c>. Shared with the contact stats tab.
    /// </summary>
    internal static string? GetSubmissionsPath(IStatsAdminLinks adminLinks, int formId) =>
        adminLinks.GetPath<FormSubmissionsTab>(new PageParameterValues
        {
            { typeof(FormEditSection), formId },
        });
}

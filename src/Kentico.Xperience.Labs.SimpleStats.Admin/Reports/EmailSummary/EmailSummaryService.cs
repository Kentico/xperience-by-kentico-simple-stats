using System.Globalization;

using CMS.Helpers;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.DigitalMarketing.UIPages;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.EmailSummary;

/// <summary>
/// Builds the email summary report.
/// </summary>
public interface IEmailSummaryService
{
    /// <summary>
    /// Returns the report for the query.
    /// </summary>
    /// <param name="query">Normalized filter. <see cref="StatsQuery.ChannelId"/> should be an email channel (others return no emails).</param>
    /// <param name="refresh">When <c>true</c>, cached data is dropped and read again from the database (then cached again).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<EmailSummaryResult> GetReport(StatsQuery query, bool refresh, CancellationToken cancellationToken);
}

internal sealed class EmailSummaryService(
    IEmailSummaryRepository repository,
    IProgressiveCache cache,
    IStatsCacheInvalidator cacheInvalidator,
    IStatsAdminLinks adminLinks,
    TimeProvider clock) : IEmailSummaryService
{
    /// <summary>
    /// Slug prefix of the native email channel applications (<c>emails-{EmailChannelID}</c>). The product constant
    /// (<c>EmailPagePathSlugs.APPLICATION</c>) and its slug helper are internal, so the prefix is repeated here.
    /// </summary>
    internal const string EmailApplicationSlugPrefix = "emails";

    private readonly IEmailSummaryRepository repository = repository;
    private readonly IProgressiveCache cache = cache;
    private readonly IStatsCacheInvalidator cacheInvalidator = cacheInvalidator;
    private readonly IStatsAdminLinks adminLinks = adminLinks;
    private readonly TimeProvider clock = clock;

    public async Task<EmailSummaryResult> GetReport(StatsQuery query, bool refresh, CancellationToken cancellationToken)
    {
        var (previousFrom, _) = StatsComparison.GetPreviousRange(query);

        // Unique opens and clicks are counted per period in SQL, so the grouping is part of the key.
        var settings = StatsCache.CreateSettings(
            "email-summary",
            query.From.DayNumber,
            query.To.DayNumber,
            query.ChannelId ?? 0,
            query.Grouping);

        var snapshot = await cache.LoadAsync(
            cacheInvalidator,
            settings,
            refresh,
            async token => new EmailSummarySnapshot(
                await repository.GetData(previousFrom, query.From, query.To, query.ChannelId, query.Grouping, token),
                clock.GetUtcNow()),
            cancellationToken);

        var result = EmailSummaryReportBuilder.Build(query, snapshot.Data, GetStatisticsPath);
        var listChannel = EmailSummaryReportBuilder.GetListChannel(result.ChannelId, snapshot.Data);

        return result with
        {
            EmailsAppPath = listChannel?.PrimaryLanguage is string language
                ? adminLinks.GetPath<EmailList>(GetChannelParameters(listChannel.EmailChannelId, language))
                : null,
            UpdatedAt = snapshot.ReadAt,
        };
    }

    /// <summary>
    /// Path of the email's Statistics tab, for example <c>/emails-1/en/list/3/statistics</c>.
    /// </summary>
    private string? GetStatisticsPath(int emailId, int? emailChannelId, string? languageName) =>
        GetStatisticsPath(adminLinks, emailId, emailChannelId, languageName);

    /// <summary>
    /// Path of the email's Statistics tab, or <c>null</c> without a channel or language. Shared with the contact stats tab.
    /// </summary>
    internal static string? GetStatisticsPath(IStatsAdminLinks adminLinks, int emailId, int? emailChannelId, string? languageName)
    {
        if (emailChannelId is not int channelId || string.IsNullOrEmpty(languageName))
        {
            return null;
        }

        var parameters = GetChannelParameters(channelId, languageName);
        parameters.Add(typeof(EmailEditLayout), emailId);

        return adminLinks.GetPath<EmailStatisticsTab>(parameters);
    }

    /// <summary>
    /// Parameters of the native email channel application and language, like the product's (internal) link helper.
    /// </summary>
    internal static PageParameterValues GetChannelParameters(int emailChannelId, string languageName) =>
        new()
        {
            { typeof(EmailChannelApplication), string.Create(CultureInfo.InvariantCulture, $"{EmailApplicationSlugPrefix}-{emailChannelId}") },
            { typeof(EmailChannelContentLanguage), languageName },
        };
}

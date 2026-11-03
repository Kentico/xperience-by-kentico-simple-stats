using CMS.ContentEngine;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.EmailSummary;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;
using Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

[assembly: UIPage(
    uiPageType: typeof(EmailSummaryPage),
    parentType: typeof(StatsEmailsSection),
    slug: "email-summary",
    name: "Email summary",
    templateName: EmailSummaryPage.TEMPLATE_NAME,
    order: 50,
    Icon = Icons.Graph)]

namespace Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

/// <summary>
/// Regular emails sent in a date range with their statistics, top and bottom performers, email activity over time and automated emails.
/// </summary>
[UIEvaluatePermission(StatsPermissions.EMAIL_SUMMARY)]
public sealed class EmailSummaryPage(
    IEmailSummaryService emailSummaryService,
    IStatsChannelOptionsProvider channelOptionsProvider,
    IPageLinkGenerator pageLinkGenerator,
    TimeProvider clock,
    IUIPermissionEvaluator permissionEvaluator,
    IStatsExportEventPublisher exportEventPublisher) : StatsReportPage<EmailSummaryClientProperties>(permissionEvaluator, exportEventPublisher)
{
    public const string TEMPLATE_NAME = "@kentico/xperience-labs-simple-stats-admin/EmailSummary";

    private static readonly ChannelType[] channelTypes = [ChannelType.Email];

    private readonly IEmailSummaryService emailSummaryService = emailSummaryService;
    private readonly IStatsChannelOptionsProvider channelOptionsProvider = channelOptionsProvider;
    private readonly IPageLinkGenerator pageLinkGenerator = pageLinkGenerator;
    private readonly TimeProvider clock = clock;

    protected override async Task<EmailSummaryClientProperties> ConfigureReportProperties(EmailSummaryClientProperties properties)
    {
        var query = new StatsFilter().Normalize(GetToday());

        properties.Report = await emailSummaryService.GetReport(query, refresh: false, CancellationToken.None);
        properties.Channels = await channelOptionsProvider.GetChannelOptions(channelTypes, CancellationToken.None);
        properties.Today = GetToday();
        properties.PagePath = pageLinkGenerator.GetPath<EmailSummaryPage>();

        return properties;
    }

    [PageCommand(CommandName = "LOAD", Permission = StatsPermissions.EMAIL_SUMMARY)]
    public async Task<ICommandResponse<EmailSummaryResult>> Load(StatsLoadRequest request, CancellationToken cancellationToken)
    {
        var channels = await channelOptionsProvider.GetChannelOptions(channelTypes, cancellationToken);
        var query = NormalizeChannel((request?.Filter ?? new StatsFilter()).Normalize(GetToday()), channels);
        var report = await emailSummaryService.GetReport(query, request?.Refresh ?? false, cancellationToken);

        return ResponseFrom(report);
    }

    /// <summary>
    /// Drops a channel that is not an email channel (all email channels), so the cache key has no channel that cannot apply.
    /// </summary>
    internal static StatsQuery NormalizeChannel(StatsQuery query, IReadOnlyList<StatsChannelOption> emailChannels) =>
        query.ChannelId is int id && emailChannels.Any(c => c.Id == id) ? query : query with { ChannelId = null };

    // EmailStatisticsHitsTime and SendConfigurationScheduledTime are server local time, compared as stored, so "today" uses the server time zone.
    private DateOnly GetToday() => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
}

public sealed class EmailSummaryClientProperties : StatsReportClientProperties
{
    public EmailSummaryResult? Report { get; set; }

    /// <summary>
    /// Email channels. The client shows the channel filter only with more than one.
    /// </summary>
    public IReadOnlyList<StatsChannelOption> Channels { get; set; } = [];

    /// <summary>
    /// Server date used for date presets.
    /// </summary>
    public DateOnly Today { get; set; }

    /// <summary>
    /// Path of this page relative to the admin root. The client uses it to turn admin paths into links.
    /// </summary>
    public string? PagePath { get; set; }
}

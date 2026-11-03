using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ActivityCounts;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContentInventory;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContentLocks;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.EmailSummary;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.EventLog;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.FormSubmissions;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Members;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.NewContacts;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.OrdersRevenue;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Overview;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PageFreshness;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PublishingCalendar;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;
using Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

[assembly: UIPage(
    uiPageType: typeof(OverviewPage),
    parentType: typeof(StatsApplicationPage),
    slug: "overview",
    name: "Overview",
    templateName: OverviewPage.TEMPLATE_NAME,
    order: 50,
    Icon = Icons.Gauge)]

namespace Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

/// <summary>
/// One-page summary of the other reports: what needs attention, what is coming up, what happened and whether it is working.
/// Each tile loads through its own page command, guarded by the permission of its source report, and calls that report's service
/// (no queries of its own). The page's own <see cref="UIEvaluatePermissionAttribute"/> is checked before every command, so each
/// command needs both permissions. Kept in its own files so it can move to its own application later.
/// </summary>
[UIEvaluatePermission(StatsPermissions.OVERVIEW)]
public sealed class OverviewPage(
    IContentInventoryService contentInventoryService,
    IContentLocksService contentLocksService,
    IPageFreshnessService pageFreshnessService,
    IPublishingCalendarService publishingCalendarService,
    IEmailSummaryService emailSummaryService,
    IFormSubmissionsService formSubmissionsService,
    INewContactsService newContactsService,
    IMembersService membersService,
    IOrdersRevenueService ordersRevenueService,
    IEventLogReportService eventLogReportService,
    IActivityCountsService activityCountsService,
    IPageLinkGenerator pageLinkGenerator,
    TimeProvider clock,
    IUIPermissionEvaluator permissionEvaluator,
    IStatsExportEventPublisher exportEventPublisher) : StatsReportPage<OverviewClientProperties>(permissionEvaluator, exportEventPublisher)
{
    public const string TEMPLATE_NAME = "@kentico/xperience-labs-simple-stats-admin/Overview";

    /// <summary>
    /// Snapshot tiles use all kinds and all channels (the same cache key as the reports' unfiltered view).
    /// </summary>
    private static readonly StatsSnapshotQuery allContent = new(null, null);

    private readonly IContentInventoryService contentInventoryService = contentInventoryService;
    private readonly IContentLocksService contentLocksService = contentLocksService;
    private readonly IPageFreshnessService pageFreshnessService = pageFreshnessService;
    private readonly IPublishingCalendarService publishingCalendarService = publishingCalendarService;
    private readonly IEmailSummaryService emailSummaryService = emailSummaryService;
    private readonly IFormSubmissionsService formSubmissionsService = formSubmissionsService;
    private readonly INewContactsService newContactsService = newContactsService;
    private readonly IMembersService membersService = membersService;
    private readonly IOrdersRevenueService ordersRevenueService = ordersRevenueService;
    private readonly IEventLogReportService eventLogReportService = eventLogReportService;
    private readonly IActivityCountsService activityCountsService = activityCountsService;
    private readonly IPageLinkGenerator pageLinkGenerator = pageLinkGenerator;
    private readonly TimeProvider clock = clock;
    private readonly IUIPermissionEvaluator permissionEvaluator = permissionEvaluator;

    /// <summary>
    /// Sends only what the page needs to open (tiles the user may see, ranges); no report data, so the page opens fast.
    /// </summary>
    protected override async Task<OverviewClientProperties> ConfigureReportProperties(OverviewClientProperties properties)
    {
        properties.Tiles = await GetVisibleTiles(permissionEvaluator);
        properties.RangeDays = OverviewReportBuilder.RangeDays;
        properties.DefaultRangeDays = OverviewReportBuilder.DefaultRangeDays;
        properties.CalendarWindow = OverviewReportBuilder.CalendarWindow;
        properties.Today = GetToday();
        properties.PagePath = pageLinkGenerator.GetPath<OverviewPage>();

        return properties;
    }

    [PageCommand(CommandName = OverviewTiles.LoadContent, Permission = StatsPermissions.CONTENT_INVENTORY)]
    public async Task<ICommandResponse<OverviewTileResult>> LoadContent(StatsSnapshotLoadRequest request, CancellationToken cancellationToken)
    {
        var report = await contentInventoryService.GetReport(allContent, request?.Refresh ?? false, cancellationToken);

        return ResponseFrom(OverviewReportBuilder.BuildContent(report, pageLinkGenerator.GetPath<ContentInventoryPage>()));
    }

    [PageCommand(CommandName = OverviewTiles.LoadLocks, Permission = StatsPermissions.CONTENT_LOCKS)]
    public async Task<ICommandResponse<OverviewTileResult>> LoadLocks(StatsSnapshotLoadRequest request, CancellationToken cancellationToken)
    {
        var report = await contentLocksService.GetReport(allContent, request?.Refresh ?? false, cancellationToken);

        return ResponseFrom(OverviewReportBuilder.BuildLocks(report, pageLinkGenerator.GetPath<ContentLocksPage>()));
    }

    [PageCommand(CommandName = OverviewTiles.LoadFreshness, Permission = StatsPermissions.PAGE_FRESHNESS)]
    public async Task<ICommandResponse<OverviewTileResult>> LoadFreshness(StatsLoadRequest request, CancellationToken cancellationToken)
    {
        var report = await pageFreshnessService.GetReport(GetRange(request), request?.Refresh ?? false, cancellationToken);

        return ResponseFrom(OverviewReportBuilder.BuildFreshness(report, pageLinkGenerator.GetPath<PageFreshnessPage>()));
    }

    [PageCommand(CommandName = OverviewTiles.LoadCalendar, Permission = StatsPermissions.PUBLISHING_CALENDAR)]
    public async Task<ICommandResponse<OverviewTileResult>> LoadCalendar(StatsSnapshotLoadRequest request, CancellationToken cancellationToken)
    {
        var query = new PublishingCalendarQuery(allContent, OverviewReportBuilder.CalendarWindow);
        var report = await publishingCalendarService.GetReport(query, request?.Refresh ?? false, cancellationToken);

        return ResponseFrom(OverviewReportBuilder.BuildCalendar(report, pageLinkGenerator.GetPath<PublishingCalendarPage>()));
    }

    [PageCommand(CommandName = OverviewTiles.LoadEmails, Permission = StatsPermissions.EMAIL_SUMMARY)]
    public async Task<ICommandResponse<OverviewTileResult>> LoadEmails(StatsLoadRequest request, CancellationToken cancellationToken)
    {
        var report = await emailSummaryService.GetReport(GetRange(request), request?.Refresh ?? false, cancellationToken);

        return ResponseFrom(OverviewReportBuilder.BuildEmails(report, pageLinkGenerator.GetPath<EmailSummaryPage>()));
    }

    [PageCommand(CommandName = OverviewTiles.LoadForms, Permission = StatsPermissions.FORM_SUBMISSIONS)]
    public async Task<ICommandResponse<OverviewTileResult>> LoadForms(StatsLoadRequest request, CancellationToken cancellationToken)
    {
        var report = await formSubmissionsService.GetReport(GetRange(request), request?.Refresh ?? false, cancellationToken);

        return ResponseFrom(OverviewReportBuilder.BuildForms(report, pageLinkGenerator.GetPath<FormSubmissionsPage>()));
    }

    [PageCommand(CommandName = OverviewTiles.LoadContacts, Permission = StatsPermissions.NEW_CONTACTS)]
    public async Task<ICommandResponse<OverviewTileResult>> LoadContacts(StatsLoadRequest request, CancellationToken cancellationToken)
    {
        var report = await newContactsService.GetReport(GetRange(request), request?.Refresh ?? false, cancellationToken);

        return ResponseFrom(OverviewReportBuilder.BuildContacts(report, pageLinkGenerator.GetPath<NewContactsPage>()));
    }

    [PageCommand(CommandName = OverviewTiles.LoadMembers, Permission = StatsPermissions.MEMBERS)]
    public async Task<ICommandResponse<OverviewTileResult>> LoadMembers(StatsLoadRequest request, CancellationToken cancellationToken)
    {
        var report = await membersService.GetReport(GetRange(request), request?.Refresh ?? false, cancellationToken);

        return ResponseFrom(OverviewReportBuilder.BuildMembers(report, pageLinkGenerator.GetPath<MembersPage>()));
    }

    [PageCommand(CommandName = OverviewTiles.LoadOrders, Permission = StatsPermissions.ORDERS_REVENUE)]
    public async Task<ICommandResponse<OverviewTileResult>> LoadOrders(StatsLoadRequest request, CancellationToken cancellationToken)
    {
        var query = new OrdersRevenueQuery(GetRange(request), OrderStatusId: null);
        var report = await ordersRevenueService.GetReport(query, request?.Refresh ?? false, cancellationToken);

        return ResponseFrom(OverviewReportBuilder.BuildOrders(report, pageLinkGenerator.GetPath<OrdersRevenuePage>()));
    }

    [PageCommand(CommandName = OverviewTiles.LoadEvents, Permission = StatsPermissions.EVENT_LOG)]
    public async Task<ICommandResponse<OverviewTileResult>> LoadEvents(StatsLoadRequest request, CancellationToken cancellationToken)
    {
        var query = new EventLogQuery(GetRange(request), EventType: null);
        var report = await eventLogReportService.GetReport(query, request?.Refresh ?? false, cancellationToken);

        return ResponseFrom(OverviewReportBuilder.BuildEvents(report, pageLinkGenerator.GetPath<EventLogPage>()));
    }

    [PageCommand(CommandName = OverviewTiles.LoadTraffic, Permission = StatsPermissions.ACTIVITY_COUNTS)]
    public async Task<ICommandResponse<OverviewTileResult>> LoadTraffic(StatsLoadRequest request, CancellationToken cancellationToken)
    {
        var report = await activityCountsService.GetReport(GetRange(request), request?.Refresh ?? false, cancellationToken);

        return ResponseFrom(OverviewReportBuilder.BuildTraffic(report, pageLinkGenerator.GetPath<ActivityCountsPage>()));
    }

    /// <summary>
    /// Returns the keys of the tiles whose source report permission the user has, in page order. One evaluation per tile.
    /// </summary>
    internal static async Task<IReadOnlyList<string>> GetVisibleTiles(IUIPermissionEvaluator evaluator)
    {
        var tiles = new List<string>();
        foreach (var tile in OverviewTiles.All)
        {
            if ((await evaluator.Evaluate(tile.Permission)).Succeeded)
            {
                tiles.Add(tile.Key);
            }
        }

        return tiles;
    }

    private StatsQuery GetRange(StatsLoadRequest? request) => OverviewReportBuilder.NormalizeRange(request?.Filter, GetToday());

    // Same rule as the other range reports: dates are compared as stored (server time), so "today" uses the server time zone.
    private DateOnly GetToday() => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
}

public sealed class OverviewClientProperties : StatsReportClientProperties
{
    /// <summary>
    /// Keys of the tiles the user may see (<c>OverviewTiles</c>), in page order. A tile needs the permission of its source report.
    /// Empty when the user has no report permission.
    /// </summary>
    public IReadOnlyList<string> Tiles { get; set; } = [];

    /// <summary>
    /// Range options in days, ending today.
    /// </summary>
    public IReadOnlyList<int> RangeDays { get; set; } = [];

    /// <summary>
    /// Range shown first, in days.
    /// </summary>
    public int DefaultRangeDays { get; set; }

    /// <summary>
    /// Days ahead of the "Coming up" list.
    /// </summary>
    public int CalendarWindow { get; set; }

    /// <summary>
    /// Server date the ranges end on.
    /// </summary>
    public DateOnly Today { get; set; }

    /// <summary>
    /// Path of this page relative to the admin root. The client uses it to turn admin paths into links.
    /// </summary>
    public string? PagePath { get; set; }
}

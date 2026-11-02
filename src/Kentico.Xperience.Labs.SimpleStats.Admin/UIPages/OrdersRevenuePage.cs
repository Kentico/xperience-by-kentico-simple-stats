using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.OrdersRevenue;
using Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

[assembly: UIPage(
    uiPageType: typeof(OrdersRevenuePage),
    parentType: typeof(StatsCommerceSection),
    slug: "orders-revenue",
    name: "Orders and revenue",
    templateName: OrdersRevenuePage.TEMPLATE_NAME,
    order: 700,
    Icon = Icons.DollarSign)]

namespace Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

/// <summary>
/// Digital commerce orders and revenue over time, orders by status and top products.
/// </summary>
[UIEvaluatePermission(StatsPermissions.ORDERS_REVENUE)]
public sealed class OrdersRevenuePage(
    IOrdersRevenueService ordersRevenueService,
    IPageLinkGenerator pageLinkGenerator,
    TimeProvider clock,
    IUIPermissionEvaluator permissionEvaluator,
    IStatsExportEventPublisher exportEventPublisher) : StatsReportPage<OrdersRevenueClientProperties>(permissionEvaluator, exportEventPublisher)
{
    public const string TEMPLATE_NAME = "@kentico/xperience-labs-simple-stats-admin/OrdersRevenue";

    private readonly IOrdersRevenueService ordersRevenueService = ordersRevenueService;
    private readonly IPageLinkGenerator pageLinkGenerator = pageLinkGenerator;
    private readonly TimeProvider clock = clock;

    protected override async Task<OrdersRevenueClientProperties> ConfigureReportProperties(OrdersRevenueClientProperties properties)
    {
        var query = new OrdersRevenueFilter().Normalize(GetToday());

        properties.Report = await ordersRevenueService.GetReport(query, refresh: false, CancellationToken.None);
        properties.Today = GetToday();
        properties.PagePath = pageLinkGenerator.GetPath<OrdersRevenuePage>();

        return properties;
    }

    [PageCommand(CommandName = "LOAD", Permission = StatsPermissions.ORDERS_REVENUE)]
    public async Task<ICommandResponse<OrdersRevenueResult>> Load(OrdersRevenueLoadRequest request, CancellationToken cancellationToken)
    {
        var query = (request?.Filter ?? new OrdersRevenueFilter()).Normalize(GetToday());
        var report = await ordersRevenueService.GetReport(query, request?.Refresh ?? false, cancellationToken);

        return ResponseFrom(report);
    }

    // OrderCreatedWhen is compared as stored (server local time), so "today" uses the server time zone.
    private DateOnly GetToday() => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
}

public sealed class OrdersRevenueClientProperties : StatsReportClientProperties
{
    public OrdersRevenueResult? Report { get; set; }

    /// <summary>
    /// Server date used for date presets.
    /// </summary>
    public DateOnly Today { get; set; }

    /// <summary>
    /// Path of this page relative to the admin root. The client uses it to turn admin paths into links.
    /// </summary>
    public string? PagePath { get; set; }
}

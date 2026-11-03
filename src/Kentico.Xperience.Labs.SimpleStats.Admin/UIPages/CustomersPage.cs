using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Customers;
using Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

[assembly: UIPage(
    uiPageType: typeof(CustomersPage),
    parentType: typeof(StatsCommerceSection),
    slug: "customers",
    name: "Customers",
    templateName: CustomersPage.TEMPLATE_NAME,
    order: 800,
    Icon = Icons.Heartshake)]

namespace Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

/// <summary>
/// Digital commerce customer growth, ordering and returning customers, customer locations and top customers.
/// </summary>
[UIEvaluatePermission(StatsPermissions.CUSTOMERS)]
public sealed class CustomersPage(
    ICustomersService customersService,
    IPageLinkGenerator pageLinkGenerator,
    TimeProvider clock,
    IUIPermissionEvaluator permissionEvaluator,
    IStatsExportEventPublisher exportEventPublisher) : StatsReportPage<CustomersClientProperties>(permissionEvaluator, exportEventPublisher)
{
    public const string TEMPLATE_NAME = "@kentico/xperience-labs-simple-stats-admin/Customers";

    private readonly ICustomersService customersService = customersService;
    private readonly IPageLinkGenerator pageLinkGenerator = pageLinkGenerator;
    private readonly TimeProvider clock = clock;

    protected override async Task<CustomersClientProperties> ConfigureReportProperties(CustomersClientProperties properties)
    {
        var query = new CustomersFilter().Normalize(GetToday());

        properties.Report = await customersService.GetReport(query, refresh: false, CancellationToken.None);
        properties.Today = GetToday();
        properties.PagePath = pageLinkGenerator.GetPath<CustomersPage>();

        return properties;
    }

    [PageCommand(CommandName = "LOAD", Permission = StatsPermissions.CUSTOMERS)]
    public async Task<ICommandResponse<CustomersResult>> Load(CustomersLoadRequest request, CancellationToken cancellationToken)
    {
        var query = (request?.Filter ?? new CustomersFilter()).Normalize(GetToday());
        var report = await customersService.GetReport(query, request?.Refresh ?? false, cancellationToken);

        return ResponseFrom(report);
    }

    // CustomerCreatedWhen and OrderCreatedWhen are compared as stored (server local time), so "today" uses the server time zone.
    private DateOnly GetToday() => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
}

public sealed class CustomersClientProperties : StatsReportClientProperties
{
    public CustomersResult? Report { get; set; }

    /// <summary>
    /// Server date used for date presets.
    /// </summary>
    public DateOnly Today { get; set; }

    /// <summary>
    /// Path of this page relative to the admin root. The client uses it to turn admin paths into links.
    /// </summary>
    public string? PagePath { get; set; }
}

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ReusableUsage;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;
using Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

[assembly: UIPage(
    uiPageType: typeof(ReusableUsagePage),
    parentType: typeof(StatsContentSection),
    slug: "reusable-usage",
    name: "Reusable content usage",
    templateName: ReusableUsagePage.TEMPLATE_NAME,
    order: 540,
    Icon = Icons.Chain)]

namespace Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

/// <summary>
/// Reusable items ranked by how many content items reference them, and where (pages, emails, reusable and headless items).
/// </summary>
[UIEvaluatePermission(StatsPermissions.REUSABLE_USAGE)]
public sealed class ReusableUsagePage(
    IReusableUsageService reusableUsageService,
    IPageLinkGenerator pageLinkGenerator,
    IUIPermissionEvaluator permissionEvaluator,
    IStatsExportEventPublisher exportEventPublisher) : StatsReportPage<ReusableUsageClientProperties>(permissionEvaluator, exportEventPublisher)
{
    public const string TEMPLATE_NAME = "@kentico/xperience-admin-labs-simple-stats/ReusableUsage";

    private readonly IReusableUsageService reusableUsageService = reusableUsageService;
    private readonly IPageLinkGenerator pageLinkGenerator = pageLinkGenerator;
    private readonly IUIPermissionEvaluator permissionEvaluator = permissionEvaluator;

    protected override async Task<ReusableUsageClientProperties> ConfigureReportProperties(ReusableUsageClientProperties properties)
    {
        properties.Report = await reusableUsageService.GetReport(null, refresh: false, CancellationToken.None);
        properties.PagePath = pageLinkGenerator.GetPath<ReusableUsagePage>();

        // The unused items are listed in the content inventory; link it only for users who may open it.
        properties.ContentInventoryPath = (await permissionEvaluator.Evaluate(StatsPermissions.CONTENT_INVENTORY)).Succeeded
            ? pageLinkGenerator.GetPath<ContentInventoryPage>()
            : null;

        return properties;
    }

    [PageCommand(CommandName = "LOAD", Permission = StatsPermissions.REUSABLE_USAGE)]
    public async Task<ICommandResponse<ReusableUsageResult>> Load(StatsSnapshotLoadRequest request, CancellationToken cancellationToken)
    {
        // Only the content type is read; an unknown content type means all.
        var report = await reusableUsageService.GetReport(request?.Filter?.ContentTypeId, request?.Refresh ?? false, cancellationToken);

        return ResponseFrom(report);
    }
}

public sealed class ReusableUsageClientProperties : StatsReportClientProperties
{
    public ReusableUsageResult? Report { get; set; }

    /// <summary>
    /// Path of this page relative to the admin root. The client uses it to turn admin paths into links.
    /// </summary>
    public string? PagePath { get; set; }

    /// <summary>
    /// Path of the content inventory report (it lists the unused items), or <c>null</c> when the user may not open it.
    /// </summary>
    public string? ContentInventoryPath { get; set; }
}

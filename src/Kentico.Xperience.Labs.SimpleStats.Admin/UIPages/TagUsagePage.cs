using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.TagUsage;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;
using Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

[assembly: UIPage(
    uiPageType: typeof(TagUsagePage),
    parentType: typeof(StatsContentSection),
    slug: "tag-usage",
    name: "Tag usage",
    templateName: TagUsagePage.TEMPLATE_NAME,
    order: 580,
    Icon = Icons.Tags)]

namespace Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

/// <summary>
/// Most used and unused tags, and content without tags per taxonomy field.
/// </summary>
[UIEvaluatePermission(StatsPermissions.TAG_USAGE)]
public sealed class TagUsagePage(
    ITagUsageService tagUsageService,
    IPageLinkGenerator pageLinkGenerator,
    IUIPermissionEvaluator permissionEvaluator,
    IStatsExportEventPublisher exportEventPublisher) : StatsReportPage<TagUsageClientProperties>(permissionEvaluator, exportEventPublisher)
{
    public const string TEMPLATE_NAME = "@kentico/xperience-admin-labs-simple-stats/TagUsage";

    private readonly ITagUsageService tagUsageService = tagUsageService;
    private readonly IPageLinkGenerator pageLinkGenerator = pageLinkGenerator;

    protected override async Task<TagUsageClientProperties> ConfigureReportProperties(TagUsageClientProperties properties)
    {
        properties.Report = await tagUsageService.GetReport(null, null, refresh: false, CancellationToken.None);
        properties.PagePath = pageLinkGenerator.GetPath<TagUsagePage>();

        return properties;
    }

    [PageCommand(CommandName = "LOAD", Permission = StatsPermissions.TAG_USAGE)]
    public async Task<ICommandResponse<TagUsageResult>> Load(StatsSnapshotLoadRequest request, CancellationToken cancellationToken)
    {
        // Kind and taxonomy are read; the report has no channel filter. An unknown kind or taxonomy means all.
        var query = (request?.Filter ?? new StatsSnapshotFilter()).Normalize(StatsContentKinds.Kinds, (_, _) => false);
        var report = await tagUsageService.GetReport(query.Kind, request?.Filter?.TaxonomyId, request?.Refresh ?? false, cancellationToken);

        return ResponseFrom(report);
    }
}

public sealed class TagUsageClientProperties : StatsReportClientProperties
{
    public TagUsageResult? Report { get; set; }

    /// <summary>
    /// Path of this page relative to the admin root. The client uses it to turn admin paths into links.
    /// </summary>
    public string? PagePath { get; set; }
}

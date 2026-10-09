using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.EditorContributions;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PublishingActivity;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;
using Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

[assembly: UIPage(
    uiPageType: typeof(EditorContributionsPage),
    parentType: typeof(StatsContentSection),
    slug: "editor-contributions",
    name: "Editor contributions",
    templateName: EditorContributionsPage.TEMPLATE_NAME,
    order: 570,
    Icon = Icons.Edit)]

namespace Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

/// <summary>
/// Content created, last modified and published per administration user. Shows per-user data, so it has its own permission.
/// </summary>
[UIEvaluatePermission(StatsPermissions.EDITOR_CONTRIBUTIONS)]
public sealed class EditorContributionsPage(
    IEditorContributionsService editorContributionsService,
    IStatsChannelOptionsProvider channelOptionsProvider,
    IPageLinkGenerator pageLinkGenerator,
    TimeProvider clock,
    IUIPermissionEvaluator permissionEvaluator,
    IStatsExportEventPublisher exportEventPublisher) : StatsReportPage<EditorContributionsClientProperties>(permissionEvaluator, exportEventPublisher)
{
    public const string TEMPLATE_NAME = "@kentico/xperience-admin-labs-simple-stats/EditorContributions";

    private readonly IEditorContributionsService editorContributionsService = editorContributionsService;
    private readonly IStatsChannelOptionsProvider channelOptionsProvider = channelOptionsProvider;
    private readonly IPageLinkGenerator pageLinkGenerator = pageLinkGenerator;
    private readonly TimeProvider clock = clock;

    protected override async Task<EditorContributionsClientProperties> ConfigureReportProperties(EditorContributionsClientProperties properties)
    {
        var channels = await channelOptionsProvider.GetChannelOptions(StatsContentKinds.ChannelTypes, CancellationToken.None);
        var query = new PublishingActivityFilter().Normalize(GetToday(), channels);

        properties.Report = await editorContributionsService.GetReport(query, refresh: false, CancellationToken.None);
        properties.Channels = channels;
        properties.Today = GetToday();
        properties.PagePath = pageLinkGenerator.GetPath<EditorContributionsPage>();

        return properties;
    }

    [PageCommand(CommandName = "LOAD", Permission = StatsPermissions.EDITOR_CONTRIBUTIONS)]
    public async Task<ICommandResponse<EditorContributionsResult>> Load(EditorContributionsLoadRequest request, CancellationToken cancellationToken)
    {
        // A channel that does not fit the kind (for example an email channel with pages) is dropped.
        var channels = await channelOptionsProvider.GetChannelOptions(StatsContentKinds.ChannelTypes, cancellationToken);
        var query = (request?.Filter ?? new PublishingActivityFilter()).Normalize(GetToday(), channels);
        var report = await editorContributionsService.GetReport(query, request?.Refresh ?? false, cancellationToken);

        return ResponseFrom(report);
    }

    // Created and modified times are compared as stored (server local time), so "today" uses the server time zone.
    private DateOnly GetToday() => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
}

public sealed class EditorContributionsClientProperties : StatsReportClientProperties
{
    public EditorContributionsResult? Report { get; set; }

    /// <summary>
    /// Website, email and headless channels. The client shows the ones that match the selected kind.
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

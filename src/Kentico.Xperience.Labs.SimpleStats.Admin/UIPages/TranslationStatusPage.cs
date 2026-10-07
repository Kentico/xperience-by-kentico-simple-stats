using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.TranslationStatus;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;
using Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

[assembly: UIPage(
    uiPageType: typeof(TranslationStatusPage),
    parentType: typeof(StatsContentSection),
    slug: "translation-status",
    name: "Translation status",
    templateName: TranslationStatusPage.TEMPLATE_NAME,
    order: 560,
    Icon = Icons.Translate)]

namespace Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

/// <summary>
/// Translations that are missing or behind the default language, per language and content type.
/// </summary>
[UIEvaluatePermission(StatsPermissions.TRANSLATION_STATUS)]
public sealed class TranslationStatusPage(
    ITranslationStatusService translationStatusService,
    IStatsChannelOptionsProvider channelOptionsProvider,
    IPageLinkGenerator pageLinkGenerator,
    IUIPermissionEvaluator permissionEvaluator,
    IStatsExportEventPublisher exportEventPublisher) : StatsReportPage<TranslationStatusClientProperties>(permissionEvaluator, exportEventPublisher)
{
    public const string TEMPLATE_NAME = "@kentico/xperience-admin-labs-simple-stats/TranslationStatus";

    private readonly ITranslationStatusService translationStatusService = translationStatusService;
    private readonly IStatsChannelOptionsProvider channelOptionsProvider = channelOptionsProvider;
    private readonly IPageLinkGenerator pageLinkGenerator = pageLinkGenerator;

    protected override async Task<TranslationStatusClientProperties> ConfigureReportProperties(TranslationStatusClientProperties properties)
    {
        var channels = await channelOptionsProvider.GetChannelOptions(StatsContentKinds.ChannelTypes, CancellationToken.None);
        var query = StatsContentKinds.Normalize(null, channels);

        properties.Report = await translationStatusService.GetReport(query, null, refresh: false, CancellationToken.None);
        properties.Channels = channels;
        properties.PagePath = pageLinkGenerator.GetPath<TranslationStatusPage>();

        return properties;
    }

    [PageCommand(CommandName = "LOAD", Permission = StatsPermissions.TRANSLATION_STATUS)]
    public async Task<ICommandResponse<TranslationStatusResult>> Load(StatsSnapshotLoadRequest request, CancellationToken cancellationToken)
    {
        // Kind and channel as in the content inventory: a channel that does not fit the kind is dropped. An unknown language means all.
        var channels = await channelOptionsProvider.GetChannelOptions(StatsContentKinds.ChannelTypes, cancellationToken);
        var query = StatsContentKinds.Normalize(request?.Filter, channels);
        var report = await translationStatusService.GetReport(query, request?.Filter?.LanguageId, request?.Refresh ?? false, cancellationToken);

        return ResponseFrom(report);
    }
}

public sealed class TranslationStatusClientProperties : StatsReportClientProperties
{
    public TranslationStatusResult? Report { get; set; }

    /// <summary>
    /// Website, email and headless channels. The client shows the ones that match the selected kind.
    /// </summary>
    public IReadOnlyList<StatsChannelOption> Channels { get; set; } = [];

    /// <summary>
    /// Path of this page relative to the admin root. The client uses it to turn admin paths into links.
    /// </summary>
    public string? PagePath { get; set; }
}

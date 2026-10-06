using CMS.ContentEngine;
using CMS.DataEngine;
using CMS.Websites;
using CMS.Websites.Internal;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Admin.Websites;
using Kentico.Xperience.Admin.Websites.UIPages;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.WebPageStats;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;
using Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

[assembly: UIPage(
    uiPageType: typeof(WebPageStatsPage),
    parentType: typeof(WebPageLayout),
    slug: WebPageStatsPage.SLUG,
    name: "Stats",
    templateName: WebPageStatsPage.TEMPLATE_NAME,
    order: WebPageStatsPage.ORDER,
    Icon = Icons.Graph)]

[assembly: PageExtender(typeof(WebPageStatsNavigationExtender))]

namespace Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

/// <summary>
/// "Stats" tab of a web page: contact activities logged for the page variant (language) being edited.
/// Shows aggregates only (counts and distinct contact counts), never contact details.
/// Requires <see cref="StatsPermissions.WEB_PAGE_STATS"/> in the "Simple Stats (Labs)" application, on top of the
/// website channel permissions that already guard <see cref="WebPageLayout"/>.
/// </summary>
internal sealed class WebPageStatsPage(
    IWebPageStatsService webPageStatsService,
    IStatsApplicationPermissionEvaluator statsPermissionEvaluator,
    IStatsExportEventPublisher exportEventPublisher,
    IInfoProvider<WebPageItemInfo> webPageItemInfoProvider,
    IInfoProvider<ContentLanguageInfo> contentLanguageInfoProvider,
    IInfoProvider<WebsiteChannelInfo> websiteChannelInfoProvider,
    IWebPageUrlRetriever webPageUrlRetriever,
    IWebsiteChannelLanguageSpecificDomainProvider languageSpecificDomainProvider,
    TimeProvider clock) : Page<WebPageStatsClientProperties>
{
    public const string SLUG = "simple-stats";
    public const string TEMPLATE_NAME = "@kentico/xperience-admin-labs-simple-stats/WebPageStats";

    /// <summary>
    /// After "root-properties" (<c>RootPropertiesTab</c>, 10100), the last tab the Websites application registers on <see cref="WebPageLayout"/>.
    /// </summary>
    public const int ORDER = 10150;

    private readonly IWebPageStatsService webPageStatsService = webPageStatsService;
    private readonly IStatsApplicationPermissionEvaluator statsPermissionEvaluator = statsPermissionEvaluator;
    private readonly IStatsExportEventPublisher exportEventPublisher = exportEventPublisher;
    private readonly IInfoProvider<WebPageItemInfo> webPageItemInfoProvider = webPageItemInfoProvider;
    private readonly IInfoProvider<ContentLanguageInfo> contentLanguageInfoProvider = contentLanguageInfoProvider;
    private readonly IInfoProvider<WebsiteChannelInfo> websiteChannelInfoProvider = websiteChannelInfoProvider;
    private readonly IWebPageUrlRetriever webPageUrlRetriever = webPageUrlRetriever;
    private readonly IWebsiteChannelLanguageSpecificDomainProvider languageSpecificDomainProvider = languageSpecificDomainProvider;
    private readonly TimeProvider clock = clock;

    // Resolved at most once per request (the page instance is created per request).
    private Task<WebPageStatsTarget?>? target;

    [PageParameter(typeof(WebPageUrlIdentifierPageModelBinder), typeof(WebPageLayout))]
    public WebPageUrlIdentifier WebPageIdentifier { get; set; } = new(string.Empty, 0);

    // The synthetic channel root has no web page item, so it shows the "object doesn't exist" screen (the tab is also hidden there).
    public override async Task<PageValidationResult> ValidatePage() =>
        new()
        {
            IsValid = await GetTarget() is not null,
            ErrorMessageKey = "base.forms.error.objectnotinitialized",
        };

    public override async Task<WebPageStatsClientProperties> ConfigureTemplateProperties(WebPageStatsClientProperties properties)
    {
        await EnsureGranted(StatsPermissions.WEB_PAGE_STATS);

        if (await GetTarget() is not WebPageStatsTarget pageTarget)
        {
            return properties;
        }

        var query = new StatsFilter().Normalize(GetToday());

        // UI guard only: the client builds the CSV from data already on the page.
        properties.CanExport = await statsPermissionEvaluator.IsGranted(StatsPermissions.EXPORT);
        properties.Report = await webPageStatsService.GetReport(pageTarget, query, refresh: false, CancellationToken.None);
        properties.Today = GetToday();

        return properties;
    }

    [PageCommand(CommandName = "LOAD")]
    public async Task<ICommandResponse<WebPageStatsResult>> Load(StatsLoadRequest request, CancellationToken cancellationToken)
    {
        await EnsureGranted(StatsPermissions.WEB_PAGE_STATS);

        var pageTarget = await GetTarget() ?? throw new InvalidOperationException("The web page or its language was not found.");
        var query = (request?.Filter ?? new StatsFilter()).Normalize(GetToday());
        var report = await webPageStatsService.GetReport(pageTarget, query, request?.Refresh ?? false, cancellationToken);

        return ResponseFrom(report);
    }

    /// <summary>
    /// Raises the <see cref="AfterExportStatsEvent"/> after the client downloaded a CSV, like <see cref="StatsReportPage{TClientProperties}.LogExport"/>.
    /// </summary>
    [PageCommand(CommandName = "LOG_EXPORT")]
    public async Task<ICommandResponse> LogExport(StatsExportLogRequest request, CancellationToken cancellationToken)
    {
        await EnsureGranted(StatsPermissions.WEB_PAGE_STATS);
        await EnsureGranted(StatsPermissions.EXPORT);

        await exportEventPublisher.Publish(GetType(), request, cancellationToken);

        return Response();
    }

    // PageCommand.Permission and UIEvaluatePermission check the website channel application, so permissions of
    // the "Simple Stats (Labs)" application are checked here.
    private async Task EnsureGranted(string permission)
    {
        if (!await statsPermissionEvaluator.IsGranted(permission))
        {
            throw new ForbiddenAccessException();
        }
    }

    private Task<WebPageStatsTarget?> GetTarget() => target ??= ResolveTarget();

    private async Task<WebPageStatsTarget?> ResolveTarget()
    {
        if (WebPageIdentifier.WebPageItemID == WebPageConstants.ROOT_NODE_ID)
        {
            return null;
        }

        var webPage = (await webPageItemInfoProvider.Get()
            .Columns(nameof(WebPageItemInfo.WebPageItemGUID), nameof(WebPageItemInfo.WebPageItemWebsiteChannelID))
            .WhereEquals(nameof(WebPageItemInfo.WebPageItemID), WebPageIdentifier.WebPageItemID)
            .GetEnumerableTypedResultAsync())
            .FirstOrDefault();

        var language = (await contentLanguageInfoProvider.Get()
            .Columns(nameof(ContentLanguageInfo.ContentLanguageID))
            .WhereEquals(nameof(ContentLanguageInfo.ContentLanguageName), WebPageIdentifier.LanguageName)
            .GetEnumerableTypedResultAsync())
            .FirstOrDefault();

        if (webPage is null || language is null)
        {
            return null;
        }

        var websiteChannel = (await websiteChannelInfoProvider.Get()
            .Columns(
                nameof(WebsiteChannelInfo.WebsiteChannelID),
                nameof(WebsiteChannelInfo.WebsiteChannelChannelID),
                nameof(WebsiteChannelInfo.WebsiteChannelLanguageRoutingMode))
            .WhereEquals(nameof(WebsiteChannelInfo.WebsiteChannelID), webPage.WebPageItemWebsiteChannelID)
            .GetEnumerableTypedResultAsync())
            .FirstOrDefault();

        if (websiteChannel is null)
        {
            return null;
        }

        bool usesLanguageDomains = websiteChannel.WebsiteChannelLanguageRoutingMode == WebsiteChannelLanguageRoutingMode.LanguageDomains;

        return new WebPageStatsTarget(
            webPage.WebPageItemGUID,
            language.ContentLanguageID,
            websiteChannel.WebsiteChannelChannelID,
            await GetFormUrlPath(),
            usesLanguageDomains ? await GetLanguageHosts(websiteChannel.WebsiteChannelID) : [],
            usesLanguageDomains);
    }

    /// <summary>
    /// Returns the hosts (domain and aliases) configured for the edited language in <see cref="WebsiteChannelDomainOptions.LanguageDomains"/>,
    /// or none when the language has no domain configured. Uses <c>CMS.Websites.Internal</c>: there is no public API that lists them.
    /// </summary>
    private async Task<IReadOnlyList<string>> GetLanguageHosts(int websiteChannelId)
    {
        try
        {
            var languageDomains = await languageSpecificDomainProvider.GetLanguageDomains(websiteChannelId, CancellationToken.None);
            var domains = languageDomains
                .Where(d => string.Equals(d.LanguageName, WebPageIdentifier.LanguageName, StringComparison.OrdinalIgnoreCase))
                .SelectMany(d => d.Aliases.Prepend(d.Domain));

            return [.. domains
                .Select(WebPageStatsUrlPath.NormalizeHost)
                .OfType<string>()
                .Distinct(StringComparer.Ordinal)];
        }
        catch (InvalidOperationException)
        {
            // Invalid domain configuration: match by path only, like a language without a domain.
            return [];
        }
    }

    /// <summary>
    /// Returns the normalized live URL path of the variant, or <c>null</c> when it has none (never published, or the URL cannot be built).
    /// The retriever returns <c>~/</c> (or <c>~/{language}</c>) for the channel home page, which is where the home page is served.
    /// </summary>
    private async Task<string?> GetFormUrlPath()
    {
        try
        {
            var url = await webPageUrlRetriever.Retrieve(WebPageIdentifier.WebPageItemID, WebPageIdentifier.LanguageName, forPreview: false);

            return WebPageStatsUrlPath.Normalize(url?.RelativePath);
        }
        catch (InvalidOperationException)
        {
            // Form submissions are then not matched; other activities still are.
            return null;
        }
    }

    // Same rule as the activity counts report: ActivityCreated is compared as stored, so "today" uses the server time zone.
    private DateOnly GetToday() => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
}

internal sealed class WebPageStatsClientProperties : StatsReportClientProperties
{
    public WebPageStatsResult? Report { get; set; }

    /// <summary>
    /// Server date used for date presets.
    /// </summary>
    public DateOnly Today { get; set; }
}

/// <summary>
/// Hides the "Stats" tab on the website channel root (no web page behind it) and for users without
/// <see cref="StatsPermissions.WEB_PAGE_STATS"/>. Only the navigation entry is removed; the page itself checks the permission too.
/// </summary>
internal sealed class WebPageStatsNavigationExtender(IStatsApplicationPermissionEvaluator statsPermissionEvaluator) : PageExtender<WebPageLayout>
{
    private readonly IStatsApplicationPermissionEvaluator statsPermissionEvaluator = statsPermissionEvaluator;

    public override async Task<TemplateClientProperties> ConfigureTemplateProperties(TemplateClientProperties properties)
    {
        properties = await base.ConfigureTemplateProperties(properties);

        if (Page.WebPageIdentifier?.WebPageItemID == WebPageConstants.ROOT_NODE_ID
            || !await statsPermissionEvaluator.IsGranted(StatsPermissions.WEB_PAGE_STATS))
        {
            properties.Navigation.Items = [.. properties.Navigation.Items
                .Where(item => !string.Equals(item.Path, WebPageStatsPage.SLUG, StringComparison.OrdinalIgnoreCase))];
        }

        return properties;
    }
}

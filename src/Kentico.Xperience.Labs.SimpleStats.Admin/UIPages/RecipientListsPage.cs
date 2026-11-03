using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.RecipientLists;
using Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

[assembly: UIPage(
    uiPageType: typeof(RecipientListsPage),
    parentType: typeof(StatsEmailsSection),
    slug: "recipient-lists",
    name: "Recipient lists",
    templateName: RecipientListsPage.TEMPLATE_NAME,
    order: 100,
    Icon = Icons.UserCheckbox)]

namespace Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

/// <summary>
/// Recipient list subscriptions and unsubscriptions over time, subscribers, lists compared and current subscriber statuses.
/// </summary>
[UIEvaluatePermission(StatsPermissions.RECIPIENT_LISTS)]
public sealed class RecipientListsPage(
    IRecipientListsService recipientListsService,
    IPageLinkGenerator pageLinkGenerator,
    TimeProvider clock,
    IUIPermissionEvaluator permissionEvaluator,
    IStatsExportEventPublisher exportEventPublisher) : StatsReportPage<RecipientListsClientProperties>(permissionEvaluator, exportEventPublisher)
{
    public const string TEMPLATE_NAME = "@kentico/xperience-labs-simple-stats-admin/RecipientLists";

    private readonly IRecipientListsService recipientListsService = recipientListsService;
    private readonly IPageLinkGenerator pageLinkGenerator = pageLinkGenerator;
    private readonly TimeProvider clock = clock;

    protected override async Task<RecipientListsClientProperties> ConfigureReportProperties(RecipientListsClientProperties properties)
    {
        var query = new RecipientListsFilter().Normalize(GetToday());

        properties.Report = await recipientListsService.GetReport(query, refresh: false, CancellationToken.None);
        properties.Today = GetToday();
        properties.PagePath = pageLinkGenerator.GetPath<RecipientListsPage>();

        return properties;
    }

    [PageCommand(CommandName = "LOAD", Permission = StatsPermissions.RECIPIENT_LISTS)]
    public async Task<ICommandResponse<RecipientListsResult>> Load(RecipientListsLoadRequest request, CancellationToken cancellationToken)
    {
        var query = (request?.Filter ?? new RecipientListsFilter()).Normalize(GetToday());
        var report = await recipientListsService.GetReport(query, request?.Refresh ?? false, cancellationToken);

        return ResponseFrom(report);
    }

    // EmailSubscriptionConfirmationDate is compared as stored (server local time), so "today" uses the server time zone.
    private DateOnly GetToday() => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
}

public sealed class RecipientListsClientProperties : StatsReportClientProperties
{
    public RecipientListsResult? Report { get; set; }

    /// <summary>
    /// Server date used for date presets.
    /// </summary>
    public DateOnly Today { get; set; }

    /// <summary>
    /// Path of this page relative to the admin root. The client uses it to turn admin paths into links.
    /// </summary>
    public string? PagePath { get; set; }
}

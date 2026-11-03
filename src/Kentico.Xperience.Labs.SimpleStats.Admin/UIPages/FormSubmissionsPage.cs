using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.FormSubmissions;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;
using Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

[assembly: UIPage(
    uiPageType: typeof(FormSubmissionsPage),
    parentType: typeof(StatsContactsSection),
    slug: "form-submissions",
    name: "Form submissions",
    templateName: FormSubmissionsPage.TEMPLATE_NAME,
    order: 400,
    Icon = Icons.Form)]

namespace Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

/// <summary>
/// Form submissions per form over time, most and least used forms.
/// </summary>
[UIEvaluatePermission(StatsPermissions.FORM_SUBMISSIONS)]
public sealed class FormSubmissionsPage(
    IFormSubmissionsService formSubmissionsService,
    IPageLinkGenerator pageLinkGenerator,
    TimeProvider clock,
    IUIPermissionEvaluator permissionEvaluator,
    IStatsExportEventPublisher exportEventPublisher) : StatsReportPage<FormSubmissionsClientProperties>(permissionEvaluator, exportEventPublisher)
{
    public const string TEMPLATE_NAME = "@kentico/xperience-labs-simple-stats-admin/FormSubmissions";

    private readonly IFormSubmissionsService formSubmissionsService = formSubmissionsService;
    private readonly IPageLinkGenerator pageLinkGenerator = pageLinkGenerator;
    private readonly TimeProvider clock = clock;

    protected override async Task<FormSubmissionsClientProperties> ConfigureReportProperties(FormSubmissionsClientProperties properties)
    {
        var query = new StatsFilter().Normalize(GetToday());

        properties.Report = await formSubmissionsService.GetReport(query, refresh: false, CancellationToken.None);
        properties.Today = GetToday();
        properties.PagePath = pageLinkGenerator.GetPath<FormSubmissionsPage>();

        return properties;
    }

    [PageCommand(CommandName = "LOAD", Permission = StatsPermissions.FORM_SUBMISSIONS)]
    public async Task<ICommandResponse<FormSubmissionsResult>> Load(StatsLoadRequest request, CancellationToken cancellationToken)
    {
        var query = (request?.Filter ?? new StatsFilter()).Normalize(GetToday());
        var report = await formSubmissionsService.GetReport(query, request?.Refresh ?? false, cancellationToken);

        return ResponseFrom(report);
    }

    // FormInserted is compared as stored (no time zone conversion), so "today" uses the server time zone.
    private DateOnly GetToday() => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
}

public sealed class FormSubmissionsClientProperties : StatsReportClientProperties
{
    public FormSubmissionsResult? Report { get; set; }

    /// <summary>
    /// Server date used for date presets.
    /// </summary>
    public DateOnly Today { get; set; }

    /// <summary>
    /// Path of this page relative to the admin root. The client uses it to turn admin paths into links.
    /// </summary>
    public string? PagePath { get; set; }
}

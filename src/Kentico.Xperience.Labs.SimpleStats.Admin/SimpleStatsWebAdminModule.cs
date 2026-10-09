using CMS.Core;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ActivityCounts;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.CampaignSources;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Consents;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContentInventory;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContentLocks;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Customers;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.EditorContributions;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.EmailSummary;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.EventLog;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.FormSubmissions;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Members;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.NewContacts;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.OrdersRevenue;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PageFreshness;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PublishingActivity;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PublishingCalendar;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.RecipientLists;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ReusableUsage;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.TagUsage;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.TopPages;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.TranslationStatus;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.WebPageStats;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;
using Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

[assembly: CMS.RegisterModule(typeof(Kentico.Xperience.Labs.SimpleStats.Admin.SimpleStatsWebAdminModule))]

namespace Kentico.Xperience.Labs.SimpleStats.Admin;

internal sealed class SimpleStatsWebAdminModule : AdminModule
{
    public SimpleStatsWebAdminModule()
        : base("Kentico.Xperience.Labs.SimpleStats.Admin")
    {
    }

    protected override void OnPreInit(ModulePreInitParameters parameters)
    {
        base.OnPreInit(parameters);

        RegisterServices(parameters.Services);
    }

    protected override void OnInit()
    {
        base.OnInit();

        RegisterClientModule("kentico", "xperience-admin-labs-simple-stats");
    }

    internal static void RegisterServices(IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddTransient<IStatsChannelOptionsProvider, StatsChannelOptionsProvider>();
        services.TryAddTransient<IActivityCountsRepository, ActivityCountsRepository>();
        services.TryAddTransient<IStatsCacheInvalidator, StatsCacheInvalidator>();
        services.TryAddTransient<IActivityCountsService, ActivityCountsService>();
        services.TryAddTransient<ITopPagesRepository, TopPagesRepository>();
        services.TryAddTransient<ITopPagesService, TopPagesService>();
        services.TryAddTransient<IStatsUtmDataRepository, StatsUtmDataRepository>();
        services.TryAddTransient<ICampaignSourcesRepository, CampaignSourcesRepository>();
        services.TryAddTransient<ICampaignSourcesService, CampaignSourcesService>();
        services.TryAddTransient<INewContactsRepository, NewContactsRepository>();
        services.TryAddTransient<INewContactsService, NewContactsService>();
        services.TryAddTransient<IStatsAdminLinks, StatsAdminLinks>();
        services.TryAddTransient<IFormSubmissionsRepository, FormSubmissionsRepository>();
        services.TryAddTransient<IFormSubmissionsService, FormSubmissionsService>();
        services.TryAddTransient<IContentInventoryRepository, ContentInventoryRepository>();
        services.TryAddTransient<IContentInventoryService, ContentInventoryService>();
        services.TryAddTransient<IPublishingCalendarRepository, PublishingCalendarRepository>();
        services.TryAddTransient<IPublishingCalendarService, PublishingCalendarService>();
        services.TryAddTransient<IContentLocksRepository, ContentLocksRepository>();
        services.TryAddTransient<IContentLocksService, ContentLocksService>();
        services.TryAddTransient<IPageFreshnessRepository, PageFreshnessRepository>();
        services.TryAddTransient<IPageFreshnessService, PageFreshnessService>();
        services.TryAddTransient<IReusableUsageRepository, ReusableUsageRepository>();
        services.TryAddTransient<IReusableUsageService, ReusableUsageService>();
        services.TryAddTransient<IPublishingActivityRepository, PublishingActivityRepository>();
        services.TryAddTransient<IPublishingActivityService, PublishingActivityService>();
        services.TryAddTransient<ITranslationStatusRepository, TranslationStatusRepository>();
        services.TryAddTransient<ITranslationStatusService, TranslationStatusService>();
        services.TryAddTransient<IEditorContributionsRepository, EditorContributionsRepository>();
        services.TryAddTransient<IEditorContributionsService, EditorContributionsService>();
        services.TryAddTransient<ITagUsageRepository, TagUsageRepository>();
        services.TryAddTransient<ITagUsageFieldProvider, TagUsageFieldProvider>();
        services.TryAddTransient<ITagUsageService, TagUsageService>();
        services.TryAddTransient<IEventLogRepository, EventLogRepository>();
        services.TryAddTransient<IEventLogReportService, EventLogReportService>();
        services.TryAddTransient<IStatsAmountFormatter, StatsAmountFormatter>();
        services.TryAddTransient<IOrdersRevenueRepository, OrdersRevenueRepository>();
        services.TryAddTransient<IOrdersRevenueService, OrdersRevenueService>();
        services.TryAddTransient<ICustomersRepository, CustomersRepository>();
        services.TryAddTransient<ICustomersService, CustomersService>();
        services.TryAddTransient<IMembersRepository, MembersRepository>();
        services.TryAddTransient<IMembersService, MembersService>();
        services.TryAddTransient<IConsentsRepository, ConsentsRepository>();
        services.TryAddTransient<IConsentsService, ConsentsService>();
        services.TryAddTransient<IRecipientListsRepository, RecipientListsRepository>();
        services.TryAddTransient<IRecipientListsService, RecipientListsService>();
        services.TryAddTransient<IEmailSummaryRepository, EmailSummaryRepository>();
        services.TryAddTransient<IEmailSummaryService, EmailSummaryService>();
        services.TryAddTransient<IWebPageStatsRepository, WebPageStatsRepository>();
        services.TryAddTransient<IWebPageStatsService, WebPageStatsService>();
        services.TryAddTransient<IStatsApplicationPermissionEvaluator, StatsApplicationPermissionEvaluator>();
        services.TryAddTransient<IStatsUserIdAccessor, StatsUserIdAccessor>();
        services.TryAddTransient<IStatsExportEventPublisher, StatsExportEventPublisher>();
    }
}

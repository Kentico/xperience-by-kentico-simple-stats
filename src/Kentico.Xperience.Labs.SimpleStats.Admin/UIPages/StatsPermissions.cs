namespace Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

/// <summary>
/// Custom permissions of the "Simple Stats (Labs)" application: one per report page, plus <see cref="EXPORT"/>.
/// Names are stored in role assignments, so do not change them.
/// </summary>
public static class StatsPermissions
{
    private const string PREFIX = "SimpleStats.";

    public const string ACTIVITY_COUNTS = PREFIX + "ActivityCounts";
    public const string ACTIVITY_COUNTS_DISPLAY_NAME = "Activity counts";

    public const string TOP_PAGES = PREFIX + "TopPages";
    public const string TOP_PAGES_DISPLAY_NAME = "Top pages";

    public const string CAMPAIGN_SOURCES = PREFIX + "CampaignSources";
    public const string CAMPAIGN_SOURCES_DISPLAY_NAME = "Campaign sources";

    public const string NEW_CONTACTS = PREFIX + "NewContacts";
    public const string NEW_CONTACTS_DISPLAY_NAME = "New contacts";

    public const string FORM_SUBMISSIONS = PREFIX + "FormSubmissions";
    public const string FORM_SUBMISSIONS_DISPLAY_NAME = "Form submissions";

    public const string CONTENT_INVENTORY = PREFIX + "ContentInventory";
    public const string CONTENT_INVENTORY_DISPLAY_NAME = "Content inventory";

    public const string PUBLISHING_CALENDAR = PREFIX + "PublishingCalendar";
    public const string PUBLISHING_CALENDAR_DISPLAY_NAME = "Publishing calendar";

    public const string CONTENT_LOCKS = PREFIX + "ContentLocks";
    public const string CONTENT_LOCKS_DISPLAY_NAME = "Content locks";

    public const string PAGE_FRESHNESS = PREFIX + "PageFreshness";
    public const string PAGE_FRESHNESS_DISPLAY_NAME = "Page freshness";

    public const string REUSABLE_USAGE = PREFIX + "ReusableUsage";
    public const string REUSABLE_USAGE_DISPLAY_NAME = "Reusable content usage";

    public const string PUBLISHING_ACTIVITY = PREFIX + "PublishingActivity";
    public const string PUBLISHING_ACTIVITY_DISPLAY_NAME = "Publishing activity";

    public const string TRANSLATION_STATUS = PREFIX + "TranslationStatus";
    public const string TRANSLATION_STATUS_DISPLAY_NAME = "Translation status";

    public const string EDITOR_CONTRIBUTIONS = PREFIX + "EditorContributions";
    public const string EDITOR_CONTRIBUTIONS_DISPLAY_NAME = "Editor contributions";

    public const string TAG_USAGE = PREFIX + "TagUsage";
    public const string TAG_USAGE_DISPLAY_NAME = "Tag usage";

    public const string EVENT_LOG = PREFIX + "EventLog";
    public const string EVENT_LOG_DISPLAY_NAME = "Event log";

    public const string ORDERS_REVENUE = PREFIX + "OrdersRevenue";
    public const string ORDERS_REVENUE_DISPLAY_NAME = "Orders and revenue";

    public const string CUSTOMERS = PREFIX + "Customers";
    public const string CUSTOMERS_DISPLAY_NAME = "Customers";

    public const string MEMBERS = PREFIX + "Members";
    public const string MEMBERS_DISPLAY_NAME = "Member registrations";

    public const string CONSENTS = PREFIX + "Consents";
    public const string CONSENTS_DISPLAY_NAME = "Consents";

    public const string EMAIL_SUMMARY = PREFIX + "EmailSummary";
    public const string EMAIL_SUMMARY_DISPLAY_NAME = "Email summary";

    public const string RECIPIENT_LISTS = PREFIX + "RecipientLists";
    public const string RECIPIENT_LISTS_DISPLAY_NAME = "Recipient lists";

    /// <summary>
    /// Shows the "Stats (Labs)" tab of web pages in website channels. Not tied to a page of this application.
    /// </summary>
    public const string WEB_PAGE_STATS = PREFIX + "WebPageStats";
    public const string WEB_PAGE_STATS_DISPLAY_NAME = "Web page stats";

    /// <summary>
    /// Shows the "Stats (Labs)" tab of contacts in Contact management (per-contact behavior data). Not tied to a page of this application.
    /// </summary>
    public const string CONTACT_STATS = PREFIX + "ContactStats";
    public const string CONTACT_STATS_DISPLAY_NAME = "Contact stats";

    /// <summary>
    /// Shows "Export CSV" in all reports. Not tied to a report page.
    /// </summary>
    public const string EXPORT = PREFIX + "Export";
    public const string EXPORT_DISPLAY_NAME = "Export";
}

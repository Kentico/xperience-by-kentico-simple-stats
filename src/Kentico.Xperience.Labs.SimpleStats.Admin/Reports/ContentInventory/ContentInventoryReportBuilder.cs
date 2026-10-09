using System.Globalization;

using CMS.ContentEngine;
using CMS.DataEngine;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContentInventory;

/// <summary>
/// Turns content aggregates into the content inventory report.
/// </summary>
internal static class ContentInventoryReportBuilder
{
    /// <inheritdoc cref="StatsContentKinds.Kinds"/>
    public static IReadOnlyList<string> Kinds => StatsContentKinds.Kinds;

    /// <inheritdoc cref="StatsContentKinds.ChannelTypes"/>
    public static IReadOnlyList<ChannelType> ChannelTypes => StatsContentKinds.ChannelTypes;

    /// <summary>
    /// Days a language variant can wait unchanged in a workflow step before it counts as waiting too long.
    /// Also the days a newer draft of a published variant can stay unchanged before it counts as a forgotten edit.
    /// </summary>
    public const int OverdueDays = 14;

    /// <summary>
    /// Rows of each item list (oldest variants, workflow, unused reusable items).
    /// </summary>
    public const int ListLimit = 25;

    /// <summary>
    /// Months without a change after which a language variant is stale (the "Over 12 months" age bucket).
    /// See <see cref="ContentInventorySql.GetStaleBefore"/>.
    /// </summary>
    public const int StaleMonths = 12;

    // Status keys, also used by the client.
    public const string PublishedKey = "published";
    public const string DraftKey = "draft";
    public const string WorkflowKey = "workflow";
    public const string UnpublishedKey = "unpublished";
    public const string OtherKey = "other";

    // Age bucket keys.
    public const string Under3MonthsKey = "under-3-months";
    public const string Months3To6Key = "3-6-months";
    public const string Months6To12Key = "6-12-months";
    public const string Over12MonthsKey = "over-12-months";

    /// <inheritdoc cref="StatsContentKinds.GetChannelType"/>
    public static ChannelType? GetChannelType(string? kind) => StatsContentKinds.GetChannelType(kind);

    /// <inheritdoc cref="StatsContentKinds.IsChannelAllowed"/>
    public static bool IsChannelAllowed(string? kind, int channelId, IEnumerable<StatsChannelOption> channels) =>
        StatsContentKinds.IsChannelAllowed(kind, channelId, channels);

    /// <summary>
    /// Returns <c>true</c> when the filters can match reusable items (all kinds or reusable, no channel).
    /// </summary>
    public static bool IncludesReusable(StatsSnapshotQuery query) =>
        query.ChannelId is null && (query.Kind is null || query.Kind == ClassContentTypeType.REUSABLE);

    /// <summary>
    /// Builds the report.
    /// </summary>
    /// <param name="query">Normalized filter.</param>
    /// <param name="data">Aggregates for the filter.</param>
    /// <param name="getContentTypePath">Returns the admin path of a content type by class ID, or <c>null</c>.</param>
    /// <param name="getWorkflowPath">Returns the admin path of a workflow's steps by workflow ID, or <c>null</c>.</param>
    /// <param name="getContentItemPath">Returns the admin path of an item where it is edited (Content hub or channel application), or <c>null</c>. Items without link data get no link.</param>
    public static ContentInventoryResult Build(
        StatsSnapshotQuery query,
        ContentInventoryData data,
        Func<int, string?> getContentTypePath,
        Func<int, string?> getWorkflowPath,
        Func<ContentItemLink, string?>? getContentItemPath = null)
    {
        var contentTypes = data.ContentTypes
            .GroupBy(t => t.ClassId)
            .Select(g => g.First())
            .ToList();

        int totalItems = contentTypes.Sum(t => Math.Max(t.ItemCount, 0));

        var byContentType = StatsRankedBuilder.BuildSnapshot(
            query.ChannelId,
            contentTypes.Select(t => ToEntry(t, getContentTypePath)),
            totalItems,
            contentTypes.Count,
            limit: contentTypes.Count,
            includeZero: true);

        var byKind = StatsRankedBuilder.BuildSnapshot(
            query.ChannelId,
            contentTypes
                .GroupBy(t => t.Kind, StringComparer.OrdinalIgnoreCase)
                .Select(g => new StatsRankedEntry(g.Key, g.Key, null, g.Sum(t => t.ItemCount), g.Count(), null)),
            totalItems,
            0,
            limit: Kinds.Count + 1);

        var statuses = data.Statuses.Where(s => s.VariantCount > 0).ToList();
        int totalVariants = statuses.Sum(s => s.VariantCount);

        var byStatus = StatsRankedBuilder.BuildSnapshot(
            query.ChannelId,
            BuildStatusEntries(statuses),
            totalVariants,
            0,
            limit: int.MaxValue,
            includeZero: true,
            keepOrder: true);

        var languages = data.Languages
            .GroupBy(l => l.LanguageId)
            .Select(g => g.First())
            .OrderByDescending(l => l.IsDefault)
            .ThenBy(l => l.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        string? itemPath(ContentItemLink? link) => link is null || getContentItemPath is null ? null : getContentItemPath(link);

        var coverage = languages
            .Select(l => new StatsCoverageItem(
                l.CodeName,
                string.IsNullOrWhiteSpace(l.DisplayName) ? l.CodeName : l.DisplayName,
                l.CodeName,
                Math.Clamp(l.ItemCount, 0, totalItems),
                totalItems))
            .ToList();

        int inWorkflow = statuses.Where(s => s.StepId is not null).Sum(s => s.VariantCount);

        return new(
            query.Kind,
            query.ChannelId,
            totalItems,
            byKind,
            byContentType,
            byStatus,
            coverage,
            BuildAge(query, data, itemPath),
            BuildWorkflow(data, inWorkflow, getWorkflowPath, itemPath),
            BuildForgottenEdits(data, getWorkflowPath, itemPath),
            IncludesReusable(query) ? BuildUnused(data, contentTypes, getContentTypePath, itemPath) : null,
            totalVariants,
            statuses.Sum(s => s.ScheduledPublish),
            statuses.Sum(s => s.ScheduledUnpublish),
            contentTypes.Count,
            contentTypes.Count(t => t.ItemCount > 0),
            languages.FirstOrDefault(l => l.IsDefault)?.DisplayName);
    }

    /// <summary>
    /// Returns the status category of a language variant. A variant in a workflow step is "in workflow" whatever its version status.
    /// </summary>
    public static string GetStatusKey(int versionStatus, int? stepId)
    {
        if (stepId is not null)
        {
            return WorkflowKey;
        }

        var status = (VersionStatus)versionStatus;
        if (!Enum.IsDefined(status))
        {
            return OtherKey;
        }

        return status switch
        {
            VersionStatus.Published => PublishedKey,
            VersionStatus.InitialDraft or VersionStatus.Draft => DraftKey,
            VersionStatus.Unpublished => UnpublishedKey,
            _ => OtherKey,
        };
    }

    private static StatsRankedEntry ToEntry(ContentTypeRow type, Func<int, string?> getContentTypePath) =>
        new(
            Key: type.CodeName,
            Label: string.IsNullOrWhiteSpace(type.DisplayName) ? type.CodeName : type.DisplayName,
            SecondaryLabel: type.Kind,
            Value: type.ItemCount,
            SecondaryValue: null,
            Url: null)
        {
            AdminPath = getContentTypePath(type.ClassId),
        };

    private static ContentAgeSummary BuildAge(StatsSnapshotQuery query, ContentInventoryData data, Func<ContentItemLink?, string?> itemPath)
    {
        var age = data.Age;
        int total = age.Under3Months + age.Months3To6 + age.Months6To12 + age.Over12Months;

        var buckets = StatsRankedBuilder.BuildSnapshot(
            query.ChannelId,
            GetAgeEntries(age),
            total,
            0,
            limit: int.MaxValue,
            includeZero: true,
            keepOrder: true);

        var oldest = data.Oldest
            .Select(v => ToAgedItem(v, data.Now) with { AdminPath = itemPath(v.Link) })
            .ToList();

        return new(buckets, Math.Max(age.Over12Months, 0), oldest);
    }

    /// <summary>
    /// Age buckets in a fixed order (under 3 months, 3–6, 6–12, over <see cref="StaleMonths"/> months), 0 included.
    /// </summary>
    /// <param name="counts">Value of each bucket (for example language variants).</param>
    /// <param name="secondary">Optional secondary value of each bucket (for example page visits).</param>
    public static IReadOnlyList<StatsRankedEntry> GetAgeEntries(ContentAgeRow counts, ContentAgeRow? secondary = null) =>
    [
        new(Under3MonthsKey, "Under 3 months", null, counts.Under3Months, secondary?.Under3Months, null),
        new(Months3To6Key, "3–6 months", null, counts.Months3To6, secondary?.Months3To6, null),
        new(Months6To12Key, "6–12 months", null, counts.Months6To12, secondary?.Months6To12, null),
        new(Over12MonthsKey, "Over 12 months", null, counts.Over12Months, secondary?.Over12Months, null),
    ];

    private static ContentWorkflowSummary BuildWorkflow(
        ContentInventoryData data,
        int inWorkflow,
        Func<int, string?> getWorkflowPath,
        Func<ContentItemLink?, string?> itemPath)
    {
        var items = data.WorkflowItems
            .Select(v => ToAgedItem(v, data.Now) with
            {
                Detail = GetStepText(v),
                // The item itself (reusable items), else the steps of its workflow.
                AdminPath = itemPath(v.Link) ?? (v.WorkflowId is int workflowId ? getWorkflowPath(workflowId) : null),
            })
            .ToList();

        return new(Math.Max(inWorkflow, items.Count), Math.Max(data.WorkflowOverdue, 0), OverdueDays, items);
    }

    private static ContentForgottenEditsSummary BuildForgottenEdits(
        ContentInventoryData data,
        Func<int, string?> getWorkflowPath,
        Func<ContentItemLink?, string?> itemPath)
    {
        var items = data.PendingDrafts
            .Select(v => ToAgedItem(v, data.Now) with
            {
                Detail = GetLiveText(v),
                AdminPath = itemPath(v.Link) ?? (v.WorkflowId is int workflowId ? getWorkflowPath(workflowId) : null),
                Channel = StatsContentChannels.GetLabel(v.Channel, v.IsReusable, v.Workspace),
            })
            .ToList();

        int pending = Math.Max(data.PendingDraftCount, items.Count);

        return new(pending, Math.Clamp(data.ForgottenEditCount, 0, pending), OverdueDays, items);
    }

    /// <summary>
    /// "Live since yyyy-MM-dd" (the last publish of the published version), plus the workflow step when the draft is in one.
    /// </summary>
    private static string? GetLiveText(ContentVariantRow variant)
    {
        string? live = variant.LivePublishedWhen is DateTime published
            ? $"Live since {DateOnly.FromDateTime(published).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}"
            : null;
        string? step = GetStepText(variant);

        return (live, step) switch
        {
            (string l, string s) => $"{l} · {s}",
            (string l, null) => l,
            (null, var s) => s,
        };
    }

    /// <summary>
    /// "Step (Workflow)", or whichever of the two is known.
    /// </summary>
    private static string? GetStepText(ContentVariantRow variant) =>
        (variant.StepDisplayName, variant.WorkflowDisplayName) switch
        {
            (string step, string workflow) => $"{step} ({workflow})",
            (string step, null) => step,
            (null, var workflow) => workflow,
        };

    private static UnusedReusableSummary BuildUnused(
        ContentInventoryData data,
        IReadOnlyList<ContentTypeRow> contentTypes,
        Func<int, string?> getContentTypePath,
        Func<ContentItemLink?, string?> itemPath)
    {
        var unusedTypes = data.UnusedByContentType
            .GroupBy(t => t.ClassId)
            .Select(g => g.First())
            .ToList();

        int count = unusedTypes.Sum(t => Math.Max(t.ItemCount, 0));

        var byContentType = StatsRankedBuilder.BuildSnapshot(
            null,
            unusedTypes.Select(t => ToEntry(t, getContentTypePath) with { SecondaryLabel = null }),
            count,
            unusedTypes.Count,
            limit: unusedTypes.Count);

        var items = data.UnusedItems
            .Select(i => new StatsAgedItem(
                i.ItemId.ToString(CultureInfo.InvariantCulture),
                i.DisplayName,
                i.ContentType,
                null,
                null,
                DateOnly.FromDateTime(i.ModifiedWhen ?? data.Now),
                i.ModifiedWhen is DateTime modified ? StatsAgedItem.GetDays(modified, data.Now) : 0)
            {
                AdminPath = itemPath(i.Link),
            })
            .ToList();

        int reusableItems = contentTypes
            .Where(t => t.Kind == ClassContentTypeType.REUSABLE)
            .Sum(t => Math.Max(t.ItemCount, 0));

        return new(count, Math.Max(reusableItems, count), byContentType, items);
    }

    private static StatsAgedItem ToAgedItem(ContentVariantRow variant, DateTime now) =>
        new(
            variant.VariantId.ToString(CultureInfo.InvariantCulture),
            variant.DisplayName,
            variant.ContentType,
            variant.Language,
            null,
            DateOnly.FromDateTime(variant.ModifiedWhen),
            StatsAgedItem.GetDays(variant.ModifiedWhen, now));

    /// <summary>
    /// Fixed categories (0 included, so chart colors do not move between filters); "Other" only when it has variants.
    /// </summary>
    private static IEnumerable<StatsRankedEntry> BuildStatusEntries(IReadOnlyList<ContentStatusRow> statuses)
    {
        var counts = statuses
            .GroupBy(s => GetStatusKey(s.VersionStatus, s.StepId))
            .ToDictionary(g => g.Key, g => g.Sum(s => s.VariantCount));

        (string Key, string Label)[] categories =
        [
            (PublishedKey, "Published"),
            (DraftKey, "Draft"),
            (WorkflowKey, "In workflow"),
            (UnpublishedKey, "Unpublished"),
            (OtherKey, "Other"),
        ];

        foreach (var (key, label) in categories)
        {
            int count = counts.GetValueOrDefault(key);
            if (key != OtherKey || count > 0)
            {
                yield return new StatsRankedEntry(key, label, null, count, null, null);
            }
        }
    }
}

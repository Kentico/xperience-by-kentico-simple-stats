using System.Globalization;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.TranslationStatus;

/// <summary>
/// Turns language variants and their default variants into the translation status report.
/// </summary>
internal static class TranslationStatusReportBuilder
{
    /// <summary>
    /// Minutes a variant can be older than the default variant and still be up to date. Covers variants saved together
    /// (for example by seeding, imports or bulk saves), which are a few milliseconds apart.
    /// </summary>
    public const int ToleranceMinutes = 60;

    /// <summary>
    /// Rows of the outdated variants list.
    /// </summary>
    public const int ListLimit = 50;

    /// <summary>
    /// Language filter options: the non-default languages, by display name.
    /// </summary>
    /// <param name="languages">All languages.</param>
    public static IReadOnlyList<TranslationLanguageOption> GetLanguageOptions(IEnumerable<TranslationLanguageRow> languages) =>
    [
        .. languages
            .Where(l => !l.IsDefault)
            .DistinctBy(l => l.LanguageId)
            .Select(l => new TranslationLanguageOption(l.LanguageId, l.DisplayName, l.CodeName))
            .OrderBy(o => o.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(o => o.Id),
    ];

    /// <summary>
    /// Returns <paramref name="languageId"/> when it is one of <paramref name="options"/>, else <c>null</c> (all non-default languages).
    /// </summary>
    public static int? NormalizeLanguage(int? languageId, IEnumerable<TranslationLanguageOption> options) =>
        languageId is int id && id > 0 && options.Any(o => o.Id == id) ? id : null;

    /// <summary>
    /// Builds the report.
    /// </summary>
    /// <param name="query">Normalized kind and channel filter.</param>
    /// <param name="languageId">Language filter, or <c>null</c> for all non-default languages. Normalized here against the data's languages.</param>
    /// <param name="data">Data for the kind and channel filter (all languages).</param>
    /// <param name="getContentItemPath">Returns the admin path of an item where it is edited, or <c>null</c>. Items without link data get no link.</param>
    /// <param name="getContentTypePath">Returns the admin path of a content type by class ID, or <c>null</c>.</param>
    public static TranslationStatusResult Build(
        StatsSnapshotQuery query,
        int? languageId,
        TranslationStatusData data,
        Func<ContentItemLink, string?>? getContentItemPath = null,
        Func<int, string?>? getContentTypePath = null)
    {
        var options = GetLanguageOptions(data.Languages);
        int? language = NormalizeLanguage(languageId, options);
        var selected = options.Where(o => language is null || o.Id == language).ToList();
        var selectedIds = selected.Select(o => o.Id).ToHashSet();

        var counts = data.Counts.Where(c => selectedIds.Contains(c.LanguageId)).ToList();

        var languages = selected
            .Select(option =>
            {
                var rows = counts.Where(c => c.LanguageId == option.Id).ToList();
                int total = Math.Max(rows.Sum(c => c.ItemCount), 0);
                int translated = Math.Clamp(rows.Sum(c => c.TranslatedCount), 0, total);

                return new StatsCoverageItem(option.CodeName, option.DisplayName, option.CodeName, translated, total)
                {
                    Flagged = Math.Clamp(rows.Sum(c => c.OutdatedCount), 0, translated),
                };
            })
            .ToList();

        var byContentType = StatsRankedBuilder.BuildSnapshot(
            query.ChannelId,
            counts
                .GroupBy(c => c.ClassId)
                .Select(group =>
                {
                    var first = group.First();
                    int outdated = Math.Max(group.Sum(c => c.OutdatedCount), 0);
                    int missing = Math.Max(group.Sum(c => c.ItemCount - c.TranslatedCount), 0);
                    return (Type: first, Outdated: outdated, Missing: missing);
                })
                .Where(t => t.Outdated > 0 || t.Missing > 0)
                .OrderByDescending(t => t.Outdated)
                .ThenByDescending(t => t.Missing)
                .ThenBy(t => GetTypeLabel(t.Type), StringComparer.OrdinalIgnoreCase)
                .Select(t => new StatsRankedEntry(t.Type.CodeName, GetTypeLabel(t.Type), null, t.Outdated, t.Missing, null)
                {
                    AdminPath = getContentTypePath?.Invoke(t.Type.ClassId),
                }),
            languages.Sum(l => l.Flagged ?? 0),
            0,
            limit: int.MaxValue,
            includeZero: true,
            keepOrder: true);

        var outdated = data.Outdated
            .Where(row => selectedIds.Contains(row.LanguageId))
            .OrderByDescending(row => row.DefaultModifiedWhen - row.ModifiedWhen)
            .ThenBy(row => row.Id)
            .Take(ListLimit)
            .Select(row => new StatsAgedItem(
                row.Id.ToString(CultureInfo.InvariantCulture),
                row.DisplayName,
                string.IsNullOrWhiteSpace(row.ContentType) ? null : row.ContentType,
                row.Language,
                StatsUserLabels.GetLabel(row.UserId, row.UserName),
                DateOnly.FromDateTime(row.ModifiedWhen),
                StatsAgedItem.GetDays(row.ModifiedWhen, row.DefaultModifiedWhen))
            {
                AdminPath = row.Link is null || getContentItemPath is null ? null : getContentItemPath(row.Link),
                Channel = StatsContentChannels.GetLabel(row.Channel, row.IsReusable, row.Workspace),
                Until = DateOnly.FromDateTime(row.DefaultModifiedWhen),
            })
            .ToList();

        return new(
            language,
            query.Kind,
            query.ChannelId,
            data.Languages.FirstOrDefault(l => l.IsDefault)?.DisplayName,
            languages,
            languages.Sum(l => l.Covered),
            languages.Sum(l => l.Flagged ?? 0),
            languages.Sum(l => l.Missing),
            outdated,
            byContentType,
            options,
            ToleranceMinutes);
    }

    private static string GetTypeLabel(TranslationCountRow row) =>
        string.IsNullOrWhiteSpace(row.DisplayName) ? row.CodeName : row.DisplayName;
}

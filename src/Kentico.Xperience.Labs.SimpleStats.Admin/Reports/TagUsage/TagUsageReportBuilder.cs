using System.Globalization;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.TagUsage;

/// <summary>
/// Turns taxonomies, tag uses and the taxonomy fields of content types into the tag usage report.
/// </summary>
internal static class TagUsageReportBuilder
{
    /// <summary>
    /// Rows of the most used tags list.
    /// </summary>
    public const int ListLimit = 25;

    /// <summary>
    /// Rows of the unused tags list.
    /// </summary>
    public const int UnusedLimit = 100;

    /// <summary>
    /// Taxonomy filter options: all taxonomies, by title.
    /// </summary>
    /// <param name="taxonomies">Taxonomies read without a taxonomy filter.</param>
    public static IReadOnlyList<TagUsageTaxonomyOption> GetTaxonomyOptions(IEnumerable<TagUsageTaxonomyRow> taxonomies) =>
    [
        .. taxonomies
            .DistinctBy(t => t.Id)
            .Select(t => new TagUsageTaxonomyOption(t.Id, GetTaxonomyLabel(t), Math.Max(t.TagCount, 0)))
            .OrderBy(o => o.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(o => o.Id),
    ];

    /// <summary>
    /// Returns <paramref name="taxonomyId"/> when it is one of <paramref name="options"/>, else <c>null</c> (all taxonomies).
    /// </summary>
    public static int? NormalizeTaxonomy(int? taxonomyId, IEnumerable<TagUsageTaxonomyOption> options) =>
        taxonomyId is int id && id > 0 && options.Any(o => o.Id == id) ? id : null;

    /// <summary>
    /// Builds the report.
    /// </summary>
    /// <param name="kind">Normalized content type type, or <c>null</c> for all.</param>
    /// <param name="taxonomyId">Normalized taxonomy filter, or <c>null</c> for all.</param>
    /// <param name="data">Data for the filter.</param>
    /// <param name="fields">Taxonomy fields of all content types.</param>
    /// <param name="options">Taxonomy filter options (see <see cref="GetTaxonomyOptions"/>).</param>
    /// <param name="getTagPath">Returns the admin path of a tag by taxonomy ID and tag ID, or <c>null</c>.</param>
    public static TagUsageResult Build(
        string? kind,
        int? taxonomyId,
        TagUsageData data,
        IReadOnlyList<TagUsageFieldDefinition> fields,
        IReadOnlyList<TagUsageTaxonomyOption> options,
        Func<int, int, string?>? getTagPath = null)
    {
        var taxonomies = data.Taxonomies
            .DistinctBy(t => t.Id)
            .Where(t => taxonomyId is null || t.Id == taxonomyId)
            .ToList();

        int tagCount = taxonomies.Sum(t => Math.Max(t.TagCount, 0));
        int unusedTagCount = taxonomies.Sum(t => Math.Clamp(t.UnusedTagCount, 0, Math.Max(t.TagCount, 0)));
        int usedTagCount = taxonomies.Sum(t => Math.Max(t.UsedTagCount, 0));

        var topTagRows = data.TopTags.Where(t => t.Uses > 0).DistinctBy(t => t.TagId).ToList();
        var topTags = StatsRankedBuilder.BuildSnapshot(
            null,
            topTagRows.Select(t => new StatsRankedEntry(
                t.TagId.ToString(CultureInfo.InvariantCulture),
                t.Title,
                t.Taxonomy,
                t.Uses,
                null,
                null)
            {
                AdminPath = getTagPath?.Invoke(t.TaxonomyId, t.TagId),
            }),
            topTagRows.Sum(t => t.Uses),
            usedTagCount,
            ListLimit);

        var unusedTags = data.UnusedTags
            .DistinctBy(t => t.TagId)
            .Take(UnusedLimit)
            .Select(t => new TagUsageUnusedTag(t.TagId.ToString(CultureInfo.InvariantCulture), t.Title, t.Taxonomy, t.Parent)
            {
                AdminPath = getTagPath?.Invoke(t.TaxonomyId, t.TagId),
            })
            .ToList();

        var fieldItems = BuildFields(taxonomyId is null ? null : taxonomies.FirstOrDefault()?.Guid, taxonomyId is not null, data, fields);
        int fieldVariants = fieldItems.Sum(f => f.Total);
        int untagged = fieldItems.Sum(f => f.Missing);

        return new(
            taxonomyId,
            kind,
            taxonomies.Count,
            tagCount,
            unusedTagCount,
            fieldVariants,
            untagged,
            fieldVariants > 0 ? (double)untagged / fieldVariants : null,
            topTags,
            unusedTags,
            fieldItems,
            options);
    }

    /// <summary>
    /// One coverage row per taxonomy field of the taxonomy filter: variants of the content types with the field, and of them the ones with a tag
    /// in the field. Fields without variants are left out. Most untagged first.
    /// </summary>
    private static List<StatsCoverageItem> BuildFields(
        Guid? taxonomyGuid,
        bool hasTaxonomy,
        TagUsageData data,
        IReadOnlyList<TagUsageFieldDefinition> fields)
    {
        var types = data.ContentTypes.Where(t => t.Variants > 0).DistinctBy(t => t.ClassId).ToDictionary(t => t.ClassId);
        var tagged = data.FieldCounts
            .GroupBy(c => (c.FieldGuid, c.ClassId))
            .ToDictionary(g => g.Key, g => g.Sum(c => Math.Max(c.Tagged, 0)));

        var rows = fields
            .DistinctBy(f => f.Guid)
            .Where(f => !hasTaxonomy || (taxonomyGuid is Guid guid && f.TaxonomyGuids.Contains(guid)))
            .Select(field =>
            {
                var fieldTypes = field.ClassIds
                    .Distinct()
                    .Where(types.ContainsKey)
                    .Select(id => types[id])
                    .OrderBy(GetTypeLabel, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                int total = fieldTypes.Sum(t => t.Variants);
                int covered = fieldTypes.Sum(t => Math.Min(tagged.GetValueOrDefault((field.Guid, t.ClassId)), t.Variants));
                return (Field: field, Types: fieldTypes, Total: total, Covered: covered);
            })
            .Where(row => row.Total > 0)
            .ToList();

        var duplicateCaptions = rows
            .GroupBy(row => row.Field.Caption, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return
        [
            .. rows
                .Select(row => new StatsCoverageItem(
                    row.Field.Guid.ToString(),
                    duplicateCaptions.Contains(row.Field.Caption)
                        ? $"{row.Field.Caption} ({row.Field.Schema ?? GetTypeLabel(row.Types[0])})"
                        : row.Field.Caption,
                    GetTypesLabel(row.Field, row.Types),
                    row.Covered,
                    row.Total))
                .OrderByDescending(item => item.Missing)
                .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item.Key, StringComparer.Ordinal),
        ];
    }

    private static string GetTypesLabel(TagUsageFieldDefinition field, IEnumerable<TagUsageTypeRow> types)
    {
        string names = string.Join(", ", types.Select(GetTypeLabel));
        return field.Schema is null ? names : $"{names} (schema {field.Schema})";
    }

    private static string GetTypeLabel(TagUsageTypeRow type) =>
        string.IsNullOrWhiteSpace(type.DisplayName) ? type.CodeName : type.DisplayName;

    private static string GetTaxonomyLabel(TagUsageTaxonomyRow taxonomy) =>
        string.IsNullOrWhiteSpace(taxonomy.Title) ? taxonomy.Name : taxonomy.Title;
}

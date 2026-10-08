using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.TagUsage;

/// <summary>
/// Which tags are used, which are never used, and how much content has no tag in a taxonomy field.
/// </summary>
/// <param name="TaxonomyId">Applied taxonomy filter, or <c>null</c> for all taxonomies.</param>
/// <param name="Kind">Applied content type type filter (<c>Website</c>, <c>Reusable</c>, <c>Email</c>, <c>Headless</c>), or <c>null</c> for all.</param>
/// <param name="TaxonomyCount">Taxonomies in the filter.</param>
/// <param name="TagCount">Tags of the taxonomies in the filter (parent and child tags).</param>
/// <param name="UnusedTagCount">Of <paramref name="TagCount"/>, tags no content item has. Not limited by the content type type filter.</param>
/// <param name="FieldVariants">Sum of <see cref="StatsCoverageItem.Total"/> over <paramref name="Fields"/>: a variant counts once per taxonomy field it has.</param>
/// <param name="UntaggedVariants">Sum of <see cref="StatsCoverageItem.Missing"/> over <paramref name="Fields"/>.</param>
/// <param name="UntaggedShare"><paramref name="UntaggedVariants"/> of <paramref name="FieldVariants"/> (0-1), or <c>null</c> without taxonomy fields.</param>
/// <param name="TopTags">
/// Most used tags (up to <see cref="TagUsageReportBuilder.ListLimit"/>). <see cref="StatsRankedItem.Value"/> is the number of language variants
/// with the tag in any field. <see cref="StatsRankedItem.SecondaryLabel"/> is the taxonomy. <see cref="StatsRankedItem.AdminPath"/> opens the tag.
/// </param>
/// <param name="UnusedTags">Unused tags by taxonomy and title (up to <see cref="TagUsageReportBuilder.UnusedLimit"/>).</param>
/// <param name="Fields">
/// Per taxonomy field (by field GUID; a reusable field schema field is one row for all content types with the schema): language variants of the
/// content types with the field (<see cref="StatsCoverageItem.Total"/>), with a tag in the field (<see cref="StatsCoverageItem.Covered"/>) and
/// without (<see cref="StatsCoverageItem.Missing"/>). <see cref="StatsCoverageItem.SecondaryLabel"/> lists the content types. Most untagged first.
/// Fields of content types without variants in the filter are left out.
/// </param>
/// <param name="TaxonomyOptions">Taxonomies (the taxonomy filter), by title.</param>
public sealed record TagUsageResult(
    int? TaxonomyId,
    string? Kind,
    int TaxonomyCount,
    int TagCount,
    int UnusedTagCount,
    int FieldVariants,
    int UntaggedVariants,
    double? UntaggedShare,
    StatsRankedResult TopTags,
    IReadOnlyList<TagUsageUnusedTag> UnusedTags,
    IReadOnlyList<StatsCoverageItem> Fields,
    IReadOnlyList<TagUsageTaxonomyOption> TaxonomyOptions)
{
    /// <summary>
    /// When the data was read from the database. Can be older than the request when served from cache.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// Tag that no content item has.
/// </summary>
/// <param name="Key">Tag ID.</param>
/// <param name="Tag">Tag title.</param>
/// <param name="Taxonomy">Taxonomy title.</param>
/// <param name="Parent">Title of the parent tag, or <c>null</c> for a top-level tag.</param>
public sealed record TagUsageUnusedTag(string Key, string Tag, string Taxonomy, string? Parent)
{
    /// <summary>Path of the tag in the Taxonomies application, or <c>null</c>.</summary>
    public string? AdminPath { get; init; }
}

/// <summary>
/// Taxonomy of the taxonomy filter.
/// </summary>
/// <param name="Id">Taxonomy ID.</param>
/// <param name="DisplayName">Taxonomy title.</param>
/// <param name="TagCount">Tags of the taxonomy.</param>
public sealed record TagUsageTaxonomyOption(int Id, string DisplayName, int TagCount);

/// <summary>
/// Taxonomy with its tag counts.
/// </summary>
/// <param name="Id">Taxonomy ID.</param>
/// <param name="Guid">Taxonomy GUID (taxonomy fields store it in their settings).</param>
/// <param name="Name">Code name.</param>
/// <param name="Title">Title.</param>
/// <param name="TagCount">Tags of the taxonomy.</param>
/// <param name="UnusedTagCount">Of <paramref name="TagCount"/>, tags no content item has (all content).</param>
/// <param name="UsedTagCount">Tags with at least one use in the filtered content.</param>
internal sealed record TagUsageTaxonomyRow(int Id, Guid Guid, string Name, string Title, int TagCount, int UnusedTagCount, int UsedTagCount);

/// <summary>
/// One tag.
/// </summary>
/// <param name="TagId">Tag ID.</param>
/// <param name="TaxonomyId">Taxonomy ID.</param>
/// <param name="Title">Tag title.</param>
/// <param name="Taxonomy">Taxonomy title.</param>
/// <param name="Parent">Parent tag title, or <c>null</c>.</param>
/// <param name="Uses">Language variants with the tag in any field (0 for unused tags).</param>
internal sealed record TagUsageTagRow(int TagId, int TaxonomyId, string Title, string Taxonomy, string? Parent, int Uses);

/// <summary>
/// Language variants of one content type with a tag in one field.
/// </summary>
/// <param name="FieldGuid">Field GUID (<c>ContentItemTagFieldGUID</c>).</param>
/// <param name="ClassId">Content type (class) ID.</param>
/// <param name="Tagged">Language variants with at least one tag in the field.</param>
internal sealed record TagUsageFieldCountRow(Guid FieldGuid, int ClassId, int Tagged);

/// <summary>
/// Language variants of one content type.
/// </summary>
internal sealed record TagUsageTypeRow(int ClassId, string CodeName, string DisplayName, int Variants);

/// <summary>
/// Data read by <see cref="ITagUsageRepository"/> for one content type type and taxonomy filter.
/// </summary>
/// <param name="Taxonomies">All taxonomies. Tag counts only for the taxonomy filter (0 for other taxonomies).</param>
/// <param name="TopTags">Most used tags.</param>
/// <param name="UnusedTags">Unused tags by taxonomy and title.</param>
/// <param name="FieldCounts">Tagged variants per field and content type.</param>
/// <param name="ContentTypes">Variants per content type (types with variants only).</param>
internal sealed record TagUsageData(
    IReadOnlyList<TagUsageTaxonomyRow> Taxonomies,
    IReadOnlyList<TagUsageTagRow> TopTags,
    IReadOnlyList<TagUsageTagRow> UnusedTags,
    IReadOnlyList<TagUsageFieldCountRow> FieldCounts,
    IReadOnlyList<TagUsageTypeRow> ContentTypes)
{
    public static TagUsageData Empty { get; } = new([], [], [], [], []);
}

/// <summary>
/// Taxonomy field of content types (see <see cref="ITagUsageFieldProvider"/>).
/// </summary>
/// <param name="Guid">Field GUID. Tags are stored with it (<c>ContentItemTagFieldGUID</c>).</param>
/// <param name="Name">Field (column) name.</param>
/// <param name="Caption">Field caption, or the name without one.</param>
/// <param name="TaxonomyGuids">Taxonomies the field offers tags from.</param>
/// <param name="Schema">Display name of the reusable field schema the field belongs to, or <c>null</c> for a field of the content type.</param>
/// <param name="ClassIds">Content types (class IDs) with the field.</param>
internal sealed record TagUsageFieldDefinition(
    Guid Guid,
    string Name,
    string Caption,
    IReadOnlyList<Guid> TaxonomyGuids,
    string? Schema,
    IReadOnlyList<int> ClassIds);

/// <summary>
/// Tag usage data with the time it was read. This is the cached value.
/// </summary>
internal sealed record TagUsageSnapshot(TagUsageData Data, DateTimeOffset ReadAt);

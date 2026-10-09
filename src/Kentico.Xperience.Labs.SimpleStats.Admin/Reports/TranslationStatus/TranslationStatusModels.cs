using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.TranslationStatus;

/// <summary>
/// Translations that are missing or behind the default language, per non-default language. Counts are per item and language.
/// </summary>
/// <param name="LanguageId">Applied language filter (a non-default language), or <c>null</c> for all non-default languages.</param>
/// <param name="Kind">Applied content type type filter (<c>Website</c>, <c>Reusable</c>, <c>Email</c>, <c>Headless</c>), or <c>null</c> for all.</param>
/// <param name="ChannelId">Applied channel filter, or <c>null</c> for all.</param>
/// <param name="DefaultLanguage">Display name of the default language, or <c>null</c>.</param>
/// <param name="Languages">
/// Per selected language: items with a variant (<see cref="StatsCoverageItem.Covered"/>) of all filtered items
/// (<see cref="StatsCoverageItem.Total"/>, the same as the content inventory's language coverage), with the outdated ones in
/// <see cref="StatsCoverageItem.Flagged"/>. By display name.
/// </param>
/// <param name="TranslatedCount">Variants in the selected languages (up to date and outdated).</param>
/// <param name="OutdatedCount">Of <paramref name="TranslatedCount"/>, variants behind the default variant.</param>
/// <param name="MissingCount">Items without a variant in a selected language (one per item and language).</param>
/// <param name="Outdated">
/// Outdated variants, most days behind first (up to <see cref="TranslationStatusReportBuilder.ListLimit"/>).
/// <see cref="StatsAgedItem.Since"/> is the variant's last change, <see cref="StatsAgedItem.Until"/> the default variant's,
/// <see cref="StatsAgedItem.Days"/> the days behind and <see cref="StatsAgedItem.Detail"/> who last changed the variant.
/// </param>
/// <param name="ByContentType">
/// Outdated (<see cref="StatsRankedItem.Value"/>) and missing (<see cref="StatsRankedItem.SecondaryValue"/>) per content type,
/// most outdated first. Types with neither are left out. <see cref="StatsRankedItem.AdminPath"/> links to the content type.
/// </param>
/// <param name="LanguageOptions">Non-default languages (the language filter), by display name. Empty when only one language is set up.</param>
/// <param name="ToleranceMinutes">Minutes a variant can be older than the default variant and still be up to date.</param>
public sealed record TranslationStatusResult(
    int? LanguageId,
    string? Kind,
    int? ChannelId,
    string? DefaultLanguage,
    IReadOnlyList<StatsCoverageItem> Languages,
    int TranslatedCount,
    int OutdatedCount,
    int MissingCount,
    IReadOnlyList<StatsAgedItem> Outdated,
    StatsRankedResult ByContentType,
    IReadOnlyList<TranslationLanguageOption> LanguageOptions,
    int ToleranceMinutes)
{
    /// <summary>
    /// When the data was read from the database. Can be older than the request when served from cache.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; init; }
}

/// <summary>
/// Language of the language filter.
/// </summary>
/// <param name="Id">Content language ID.</param>
/// <param name="DisplayName">Language display name.</param>
/// <param name="CodeName">Language code name.</param>
public sealed record TranslationLanguageOption(int Id, string DisplayName, string CodeName);

/// <summary>
/// Content language.
/// </summary>
internal sealed record TranslationLanguageRow(int LanguageId, string CodeName, string DisplayName, bool IsDefault);

/// <summary>
/// Counts of one non-default language and content type.
/// </summary>
/// <param name="LanguageId">Content language ID.</param>
/// <param name="ClassId">Content type (class) ID.</param>
/// <param name="CodeName">Content type code name.</param>
/// <param name="DisplayName">Content type display name.</param>
/// <param name="ItemCount">Filtered items of the type.</param>
/// <param name="TranslatedCount">Of <paramref name="ItemCount"/>, items with a variant in the language.</param>
/// <param name="OutdatedCount">Of <paramref name="TranslatedCount"/>, variants behind the default variant.</param>
internal sealed record TranslationCountRow(
    int LanguageId,
    int ClassId,
    string CodeName,
    string DisplayName,
    int ItemCount,
    int TranslatedCount,
    int OutdatedCount);

/// <summary>
/// One outdated language variant.
/// </summary>
/// <param name="Id">Language metadata ID.</param>
/// <param name="LanguageId">Content language ID.</param>
/// <param name="DisplayName">Variant display name.</param>
/// <param name="ContentType">Content type display name.</param>
/// <param name="Language">Language display name.</param>
/// <param name="UserId">ID of the user who last changed the variant, or <c>null</c>.</param>
/// <param name="UserName">Display name of that user, or <c>null</c>.</param>
/// <param name="DefaultModifiedWhen">Last change of the default variant (server time).</param>
/// <param name="ModifiedWhen">Last change of the variant (server time).</param>
internal sealed record OutdatedVariantRow(
    int Id,
    int LanguageId,
    string DisplayName,
    string ContentType,
    string Language,
    int? UserId,
    string? UserName,
    DateTime DefaultModifiedWhen,
    DateTime ModifiedWhen)
{
    /// <inheritdoc cref="ContentItemLink"/>
    public ContentItemLink? Link { get; init; }

    /// <summary>Channel display name, or <c>null</c>.</summary>
    public string? Channel { get; init; }

    /// <summary>Whether the item is a reusable item (it has no channel, but a workspace in the Content hub).</summary>
    public bool IsReusable { get; init; }

    /// <summary>Display name of the item's workspace, or <c>null</c>.</summary>
    public string? Workspace { get; init; }
}

/// <summary>
/// Data read by <see cref="ITranslationStatusRepository"/> for all languages.
/// </summary>
/// <param name="Languages">All languages.</param>
/// <param name="Counts">Counts per non-default language and content type.</param>
/// <param name="Outdated">Outdated variants, most behind first (up to <see cref="TranslationStatusReportBuilder.ListLimit"/> per language).</param>
internal sealed record TranslationStatusData(
    IReadOnlyList<TranslationLanguageRow> Languages,
    IReadOnlyList<TranslationCountRow> Counts,
    IReadOnlyList<OutdatedVariantRow> Outdated)
{
    public static TranslationStatusData Empty { get; } = new([], [], []);
}

/// <summary>
/// Translation status data with the time it was read. This is the cached value.
/// </summary>
internal sealed record TranslationStatusSnapshot(TranslationStatusData Data, DateTimeOffset ReadAt);

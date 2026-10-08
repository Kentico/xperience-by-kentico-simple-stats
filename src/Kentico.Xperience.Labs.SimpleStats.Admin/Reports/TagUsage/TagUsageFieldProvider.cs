using System.Text.Json;

using CMS.ContentEngine.Internal;
using CMS.DataEngine;
using CMS.FormEngine;
using CMS.MacroEngine;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.TagUsage;

/// <summary>
/// Lists the taxonomy fields of all content types, including fields of reusable field schemas.
/// </summary>
internal interface ITagUsageFieldProvider
{
    /// <summary>
    /// Returns one entry per taxonomy field (by field GUID). A reusable field schema field is one entry with every content type that has the schema.
    /// </summary>
    public Task<IReadOnlyList<TagUsageFieldDefinition>> GetFields(CancellationToken cancellationToken);
}

/// <remarks>
/// Uses the product's <see cref="IContentTypeFieldProvider"/>: its fields of a content type come from the content type form merged with the
/// fields of its reusable field schemas (the form the product edits items with), so schema fields keep the GUID they have in the schema,
/// the same GUID tags are stored with. The taxonomies of a field are in its <c>TaxonomyGroup</c> setting (JSON array of taxonomy GUIDs),
/// read as the product reads it.
/// </remarks>
internal sealed class TagUsageFieldProvider(
    IContentTypeFieldProvider contentTypeFieldProvider,
    IReusableFieldSchemaManager schemaManager) : ITagUsageFieldProvider
{
    /// <summary>Setting of a taxonomy field with the GUIDs of its taxonomies.</summary>
    public const string TaxonomySetting = "TaxonomyGroup";

    private readonly IContentTypeFieldProvider contentTypeFieldProvider = contentTypeFieldProvider;
    private readonly IReusableFieldSchemaManager schemaManager = schemaManager;

    public async Task<IReadOnlyList<TagUsageFieldDefinition>> GetFields(CancellationToken cancellationToken)
    {
        var contentTypes = await DataClassInfoProvider.GetClasses()
            .WhereEquals(nameof(DataClassInfo.ClassType), ClassType.CONTENT_TYPE)
            .Columns(nameof(DataClassInfo.ClassID), nameof(DataClassInfo.ClassName))
            .GetEnumerableTypedResultAsync(cancellationToken: cancellationToken);

        var resolver = MacroResolver.GetInstance();
        var fields = new Dictionary<Guid, (FormFieldInfo Field, List<int> ClassIds)>();

        foreach (var contentType in contentTypes)
        {
            foreach (var field in contentTypeFieldProvider.GetFields(contentType.ClassName, FieldDataType.Taxonomy))
            {
                if (!fields.TryGetValue(field.Guid, out var entry))
                {
                    entry = (field, []);
                    fields[field.Guid] = entry;
                }
                entry.ClassIds.Add(contentType.ClassID);
            }
        }

        return
        [
            .. fields.Values.Select(entry => new TagUsageFieldDefinition(
                entry.Field.Guid,
                entry.Field.Name,
                GetCaption(entry.Field, resolver),
                ParseTaxonomies(entry.Field.Settings[TaxonomySetting] as string),
                GetSchemaName(entry.Field),
                entry.ClassIds)),
        ];
    }

    /// <summary>
    /// Returns the taxonomy GUIDs of a <see cref="TaxonomySetting"/> value (JSON array), or none when it is empty or invalid.
    /// </summary>
    public static IReadOnlyList<Guid> ParseTaxonomies(string? setting)
    {
        if (string.IsNullOrWhiteSpace(setting))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<Guid[]>(setting) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string GetCaption(FormFieldInfo field, MacroResolver resolver)
    {
        string caption = field.GetDisplayName(resolver);
        return string.IsNullOrWhiteSpace(caption) ? field.Name : caption;
    }

    private string? GetSchemaName(FormFieldInfo field)
    {
        if (!field.IsSchemaField())
        {
            return null;
        }

        var schema = schemaManager.Get(field.GetSchemaGuid());
        if (schema is null)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(schema.DisplayName) ? schema.Name : schema.DisplayName;
    }
}

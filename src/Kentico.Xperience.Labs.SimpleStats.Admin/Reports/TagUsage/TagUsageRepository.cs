using System.Data;
using System.Data.Common;

using CMS.DataEngine;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.TagUsage;

/// <summary>
/// Reads taxonomies, tags and tag uses from the database.
/// </summary>
internal interface ITagUsageRepository
{
    /// <summary>
    /// Returns the taxonomies, most used and unused tags, tagged variants per field and variants per content type.
    /// </summary>
    /// <param name="kind">Normalized content type type, or <c>null</c> for all.</param>
    /// <param name="taxonomyId">Normalized taxonomy ID, or <c>null</c> for all.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<TagUsageData> GetData(string? kind, int? taxonomyId, CancellationToken cancellationToken);
}

internal sealed class TagUsageRepository : ITagUsageRepository
{
    private const string ReportName = "tag usage";

    public async Task<TagUsageData> GetData(string? kind, int? taxonomyId, CancellationToken cancellationToken)
    {
        var parameters = new QueryDataParameters
        {
            new DataParameter(StatsContentSql.ClassTypeParameter, ClassType.CONTENT_TYPE),
            new DataParameter(TagUsageSql.LimitParameter, TagUsageReportBuilder.ListLimit),
            new DataParameter(TagUsageSql.UnusedLimitParameter, TagUsageReportBuilder.UnusedLimit),
        };
        if (kind is not null)
        {
            parameters.Add(new DataParameter(StatsContentSql.KindParameter, kind));
        }
        if (taxonomyId is int id)
        {
            parameters.Add(new DataParameter(TagUsageSql.TaxonomyParameter, id));
        }

        string sql = TagUsageSql.Build(kind is not null, taxonomyId is not null);

        await using var reader = await ConnectionHelper.ExecuteReaderAsync(sql, parameters, QueryTypeEnum.SQLQuery, CommandBehavior.Default, cancellationToken);

        var taxonomies = await ReadTaxonomies(reader, cancellationToken);
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var topTags = await ReadTags(reader, cancellationToken);
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var unusedTags = await ReadTags(reader, cancellationToken);
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var fieldCounts = await ReadFieldCounts(reader, cancellationToken);
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var contentTypes = await ReadContentTypes(reader, cancellationToken);

        return new TagUsageData(taxonomies, topTags, unusedTags, fieldCounts, contentTypes);
    }

    private static async Task<IReadOnlyList<TagUsageTaxonomyRow>> ReadTaxonomies(DbDataReader reader, CancellationToken cancellationToken)
    {
        int idOrdinal = reader.GetOrdinal("TaxonomyID");
        int guidOrdinal = reader.GetOrdinal("TaxonomyGUID");
        int nameOrdinal = reader.GetOrdinal("TaxonomyName");
        int titleOrdinal = reader.GetOrdinal("TaxonomyTitle");
        int tagsOrdinal = reader.GetOrdinal("TagCount");
        int unusedOrdinal = reader.GetOrdinal("UnusedTagCount");
        int usedOrdinal = reader.GetOrdinal("UsedTagCount");

        var rows = new List<TagUsageTaxonomyRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            string name = reader.GetString(nameOrdinal);
            rows.Add(new(
                reader.GetInt32(idOrdinal),
                reader.GetGuid(guidOrdinal),
                name,
                reader.IsDBNull(titleOrdinal) ? name : reader.GetString(titleOrdinal),
                reader.GetInt32(tagsOrdinal),
                reader.GetInt32(unusedOrdinal),
                reader.GetInt32(usedOrdinal)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<TagUsageTagRow>> ReadTags(DbDataReader reader, CancellationToken cancellationToken)
    {
        int idOrdinal = reader.GetOrdinal("TagID");
        int taxonomyIdOrdinal = reader.GetOrdinal("TaxonomyID");
        int titleOrdinal = reader.GetOrdinal("TagTitle");
        int taxonomyOrdinal = reader.GetOrdinal("TaxonomyTitle");
        int parentOrdinal = reader.GetOrdinal("ParentTitle");
        int usesOrdinal = reader.GetOrdinal("Uses");

        var rows = new List<TagUsageTagRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                reader.GetInt32(idOrdinal),
                reader.GetInt32(taxonomyIdOrdinal),
                reader.GetString(titleOrdinal),
                reader.IsDBNull(taxonomyOrdinal) ? string.Empty : reader.GetString(taxonomyOrdinal),
                reader.IsDBNull(parentOrdinal) ? null : reader.GetString(parentOrdinal),
                reader.GetInt32(usesOrdinal)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<TagUsageFieldCountRow>> ReadFieldCounts(DbDataReader reader, CancellationToken cancellationToken)
    {
        int fieldOrdinal = reader.GetOrdinal("FieldGUID");
        int classOrdinal = reader.GetOrdinal("ClassID");
        int taggedOrdinal = reader.GetOrdinal("Tagged");

        var rows = new List<TagUsageFieldCountRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(reader.GetGuid(fieldOrdinal), reader.GetInt32(classOrdinal), reader.GetInt32(taggedOrdinal)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<TagUsageTypeRow>> ReadContentTypes(DbDataReader reader, CancellationToken cancellationToken)
    {
        int classOrdinal = reader.GetOrdinal("ClassID");
        int nameOrdinal = reader.GetOrdinal("ClassName");
        int displayNameOrdinal = reader.GetOrdinal("ClassDisplayName");
        int variantsOrdinal = reader.GetOrdinal("Variants");

        var rows = new List<TagUsageTypeRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                reader.GetInt32(classOrdinal),
                reader.GetString(nameOrdinal),
                reader.IsDBNull(displayNameOrdinal) ? string.Empty : reader.GetString(displayNameOrdinal),
                reader.GetInt32(variantsOrdinal)));
        }

        return rows;
    }
}

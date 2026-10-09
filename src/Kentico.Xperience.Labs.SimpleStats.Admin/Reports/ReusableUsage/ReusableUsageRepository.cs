using System.Data;
using System.Data.Common;

using CMS.DataEngine;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ReusableUsage;

/// <summary>
/// Reads the usage of reusable items from the database.
/// </summary>
internal interface IReusableUsageRepository
{
    /// <summary>
    /// Returns the totals, the most used items and the usage per content type.
    /// </summary>
    /// <param name="contentTypeId">Normalized content type filter (class ID), or <c>null</c> for all reusable content types.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<ReusableUsageData> GetData(int? contentTypeId, CancellationToken cancellationToken);
}

internal sealed class ReusableUsageRepository : IReusableUsageRepository
{
    private const string ReportName = "reusable content usage";

    public async Task<ReusableUsageData> GetData(int? contentTypeId, CancellationToken cancellationToken)
    {
        var parameters = new QueryDataParameters
        {
            new DataParameter(StatsContentSql.ClassTypeParameter, ClassType.CONTENT_TYPE),
            new DataParameter(StatsContentUsageSql.ReusableKindParameter, ClassContentTypeType.REUSABLE),
            new DataParameter(StatsContentUsageSql.WebsiteKindParameter, ClassContentTypeType.WEBSITE),
            new DataParameter(StatsContentUsageSql.EmailKindParameter, ClassContentTypeType.EMAIL),
            new DataParameter(StatsContentUsageSql.HeadlessKindParameter, ClassContentTypeType.HEADLESS),
            new DataParameter(ReusableUsageSql.LimitParameter, ReusableUsageReportBuilder.ListLimit),
            new DataParameter(ReusableUsageSql.FewMaxParameter, ReusableUsageReportBuilder.FewUsagesMax),
            new DataParameter(ReusableUsageSql.SomeMaxParameter, ReusableUsageReportBuilder.SomeUsagesMax),
        };
        if (contentTypeId is int classId)
        {
            parameters.Add(new DataParameter(ReusableUsageSql.ContentTypeParameter, classId));
        }

        string sql = ReusableUsageSql.Build(contentTypeId is not null);

        await using var reader = await ConnectionHelper.ExecuteReaderAsync(sql, parameters, QueryTypeEnum.SQLQuery, CommandBehavior.Default, cancellationToken);

        var totals = await ReadTotals(reader, cancellationToken);
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var mostUsed = await ReadItems(reader, cancellationToken);
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var contentTypes = await ReadContentTypes(reader, cancellationToken);

        return new ReusableUsageData(totals, mostUsed, contentTypes);
    }

    private static async Task<ReusableUsageTotals> ReadTotals(DbDataReader reader, CancellationToken cancellationToken)
    {
        if (!await reader.ReadAsync(cancellationToken))
        {
            return ReusableUsageTotals.Empty;
        }

        int Get(string name) => reader.GetInt32(reader.GetOrdinal(name));

        return new(
            Get("ReusableItems"),
            Get("UsedItems"),
            Get("Unused"),
            Get("UsedOnce"),
            Get("UsedFew"),
            Get("UsedSome"),
            Get("UsedMany"),
            Get("Usages"));
    }

    private static async Task<IReadOnlyList<ReusableUsageRow>> ReadItems(DbDataReader reader, CancellationToken cancellationToken)
    {
        int idOrdinal = reader.GetOrdinal("ContentItemID");
        int nameOrdinal = reader.GetOrdinal("DisplayName");
        int typeOrdinal = reader.GetOrdinal("ClassDisplayName");
        int modifiedOrdinal = reader.GetOrdinal("ModifiedWhen");
        int workspaceOrdinal = reader.GetOrdinal("WorkspaceDisplayName");
        int usagesOrdinal = reader.GetOrdinal("Usages");
        int pagesOrdinal = reader.GetOrdinal("Pages");
        int emailsOrdinal = reader.GetOrdinal("Emails");
        int reusableOrdinal = reader.GetOrdinal("ReusableItems");
        int headlessOrdinal = reader.GetOrdinal("HeadlessItems");
        var links = StatsContentLinkReader.From(reader, withChannels: false);

        var rows = new List<ReusableUsageRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                reader.GetInt32(idOrdinal),
                reader.GetString(nameOrdinal),
                reader.IsDBNull(typeOrdinal) ? string.Empty : reader.GetString(typeOrdinal),
                reader.GetInt32(usagesOrdinal),
                reader.GetInt32(pagesOrdinal),
                reader.GetInt32(emailsOrdinal),
                reader.GetInt32(reusableOrdinal),
                reader.GetInt32(headlessOrdinal),
                reader.IsDBNull(modifiedOrdinal) ? null : reader.GetDateTime(modifiedOrdinal))
            {
                Link = links.Read(reader),
                Workspace = reader.IsDBNull(workspaceOrdinal) ? null : reader.GetString(workspaceOrdinal),
            });
        }

        return rows;
    }

    private static async Task<IReadOnlyList<ReusableUsageTypeRow>> ReadContentTypes(DbDataReader reader, CancellationToken cancellationToken)
    {
        int idOrdinal = reader.GetOrdinal("ClassID");
        int nameOrdinal = reader.GetOrdinal("ClassName");
        int displayNameOrdinal = reader.GetOrdinal("ClassDisplayName");
        int countOrdinal = reader.GetOrdinal("ItemCount");
        int usedOrdinal = reader.GetOrdinal("UsedItems");
        int usagesOrdinal = reader.GetOrdinal("Usages");

        var rows = new List<ReusableUsageTypeRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                reader.GetInt32(idOrdinal),
                reader.GetString(nameOrdinal),
                reader.IsDBNull(displayNameOrdinal) ? string.Empty : reader.GetString(displayNameOrdinal),
                reader.GetInt32(countOrdinal),
                reader.GetInt32(usedOrdinal),
                reader.GetInt32(usagesOrdinal)));
        }

        return rows;
    }
}

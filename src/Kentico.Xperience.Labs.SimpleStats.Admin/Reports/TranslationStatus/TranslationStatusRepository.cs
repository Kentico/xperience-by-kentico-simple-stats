using System.Data;
using System.Data.Common;

using CMS.DataEngine;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.TranslationStatus;

/// <summary>
/// Reads language variants and their default variants from the database.
/// </summary>
internal interface ITranslationStatusRepository
{
    /// <summary>
    /// Returns the languages, the counts per non-default language and content type, and the outdated variants for the query (all languages).
    /// </summary>
    /// <param name="query">Normalized kind and channel filter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<TranslationStatusData> GetData(StatsSnapshotQuery query, CancellationToken cancellationToken);
}

internal sealed class TranslationStatusRepository : ITranslationStatusRepository
{
    private const string ReportName = "translation status";

    public async Task<TranslationStatusData> GetData(StatsSnapshotQuery query, CancellationToken cancellationToken)
    {
        var parameters = new QueryDataParameters
        {
            new DataParameter(StatsContentSql.ClassTypeParameter, ClassType.CONTENT_TYPE),
            new DataParameter(TranslationStatusSql.ToleranceParameter, TranslationStatusReportBuilder.ToleranceMinutes),
            new DataParameter(TranslationStatusSql.LimitParameter, TranslationStatusReportBuilder.ListLimit),
        };
        if (query.Kind is string kind)
        {
            parameters.Add(new DataParameter(StatsContentSql.KindParameter, kind));
        }
        if (query.ChannelId is int channelId)
        {
            parameters.Add(new DataParameter(StatsContentSql.ChannelParameter, channelId));
        }

        string sql = TranslationStatusSql.Build(query.Kind is not null, query.ChannelId is not null);

        await using var reader = await ConnectionHelper.ExecuteReaderAsync(sql, parameters, QueryTypeEnum.SQLQuery, CommandBehavior.Default, cancellationToken);

        var languages = await ReadLanguages(reader, cancellationToken);
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var counts = await ReadCounts(reader, cancellationToken);
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var outdated = await ReadOutdated(reader, cancellationToken);

        return new TranslationStatusData(languages, counts, outdated);
    }

    private static async Task<IReadOnlyList<TranslationLanguageRow>> ReadLanguages(DbDataReader reader, CancellationToken cancellationToken)
    {
        int idOrdinal = reader.GetOrdinal("ContentLanguageID");
        int nameOrdinal = reader.GetOrdinal("ContentLanguageName");
        int displayNameOrdinal = reader.GetOrdinal("ContentLanguageDisplayName");
        int defaultOrdinal = reader.GetOrdinal("IsDefault");

        var rows = new List<TranslationLanguageRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            string codeName = reader.GetString(nameOrdinal);
            rows.Add(new(
                reader.GetInt32(idOrdinal),
                codeName,
                reader.IsDBNull(displayNameOrdinal) ? codeName : reader.GetString(displayNameOrdinal),
                reader.GetBoolean(defaultOrdinal)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<TranslationCountRow>> ReadCounts(DbDataReader reader, CancellationToken cancellationToken)
    {
        int languageOrdinal = reader.GetOrdinal("ContentLanguageID");
        int classOrdinal = reader.GetOrdinal("ClassID");
        int nameOrdinal = reader.GetOrdinal("ClassName");
        int displayNameOrdinal = reader.GetOrdinal("ClassDisplayName");
        int itemsOrdinal = reader.GetOrdinal("ItemCount");
        int translatedOrdinal = reader.GetOrdinal("TranslatedCount");
        int outdatedOrdinal = reader.GetOrdinal("OutdatedCount");

        var rows = new List<TranslationCountRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                reader.GetInt32(languageOrdinal),
                reader.GetInt32(classOrdinal),
                reader.GetString(nameOrdinal),
                reader.IsDBNull(displayNameOrdinal) ? string.Empty : reader.GetString(displayNameOrdinal),
                reader.GetInt32(itemsOrdinal),
                reader.GetInt32(translatedOrdinal),
                reader.GetInt32(outdatedOrdinal)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<OutdatedVariantRow>> ReadOutdated(DbDataReader reader, CancellationToken cancellationToken)
    {
        int idOrdinal = reader.GetOrdinal("VariantID");
        int languageIdOrdinal = reader.GetOrdinal("ContentLanguageID");
        int nameOrdinal = reader.GetOrdinal("DisplayName");
        int typeOrdinal = reader.GetOrdinal("ClassDisplayName");
        int languageOrdinal = reader.GetOrdinal("ContentLanguageDisplayName");
        int channelOrdinal = reader.GetOrdinal("ChannelDisplayName");
        int reusableOrdinal = reader.GetOrdinal("IsReusable");
        int workspaceOrdinal = reader.GetOrdinal("WorkspaceDisplayName");
        int userIdOrdinal = reader.GetOrdinal("UserID");
        int userNameOrdinal = reader.GetOrdinal("UserName");
        int defaultModifiedOrdinal = reader.GetOrdinal("DefaultModifiedWhen");
        int modifiedOrdinal = reader.GetOrdinal("ModifiedWhen");
        var links = StatsContentLinkReader.From(reader, withChannels: true);

        var rows = new List<OutdatedVariantRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                reader.GetInt32(idOrdinal),
                reader.GetInt32(languageIdOrdinal),
                reader.GetString(nameOrdinal),
                reader.IsDBNull(typeOrdinal) ? string.Empty : reader.GetString(typeOrdinal),
                reader.GetString(languageOrdinal),
                reader.IsDBNull(userIdOrdinal) ? null : reader.GetInt32(userIdOrdinal),
                reader.IsDBNull(userNameOrdinal) ? null : reader.GetString(userNameOrdinal),
                reader.GetDateTime(defaultModifiedOrdinal),
                reader.GetDateTime(modifiedOrdinal))
            {
                Link = links.Read(reader),
                Channel = reader.IsDBNull(channelOrdinal) ? null : reader.GetString(channelOrdinal),
                IsReusable = !reader.IsDBNull(reusableOrdinal) && reader.GetBoolean(reusableOrdinal),
                Workspace = reader.IsDBNull(workspaceOrdinal) ? null : reader.GetString(workspaceOrdinal),
            });
        }

        return rows;
    }
}

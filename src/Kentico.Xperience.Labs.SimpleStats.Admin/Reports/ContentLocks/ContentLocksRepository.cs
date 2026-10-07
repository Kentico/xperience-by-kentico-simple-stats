using System.Data;
using System.Data.Common;

using CMS.DataEngine;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContentLocks;

/// <summary>
/// Reads locked language variants from the database.
/// </summary>
internal interface IContentLocksRepository
{
    /// <summary>
    /// Returns locks per user, the locked variants (oldest lock first) and the old lock count for the query.
    /// </summary>
    /// <param name="query">Normalized filter.</param>
    /// <param name="now">Server time lock ages are counted from (lock times are compared as stored).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<ContentLocksData> GetData(StatsSnapshotQuery query, DateTime now, CancellationToken cancellationToken);
}

internal sealed class ContentLocksRepository : IContentLocksRepository
{
    private const string ReportName = "content locks";

    public async Task<ContentLocksData> GetData(StatsSnapshotQuery query, DateTime now, CancellationToken cancellationToken)
    {
        var parameters = new QueryDataParameters
        {
            new DataParameter(StatsContentSql.ClassTypeParameter, ClassType.CONTENT_TYPE),
            new DataParameter(ContentLocksSql.OldBeforeParameter, ContentLocksReportBuilder.GetOldBefore(now)),
            new DataParameter(ContentLocksSql.LimitParameter, ContentLocksReportBuilder.ListLimit),
        };
        if (query.Kind is string kind)
        {
            parameters.Add(new DataParameter(StatsContentSql.KindParameter, kind));
        }
        if (query.ChannelId is int channelId)
        {
            parameters.Add(new DataParameter(StatsContentSql.ChannelParameter, channelId));
        }

        string sql = ContentLocksSql.Build(query.Kind is not null, query.ChannelId is not null);

        await using var reader = await ConnectionHelper.ExecuteReaderAsync(sql, parameters, QueryTypeEnum.SQLQuery, CommandBehavior.Default, cancellationToken);

        var users = await ReadUsers(reader, cancellationToken);
        await StatsSql.NextResult(reader, ReportName, cancellationToken);
        var (items, oldLockCount) = await ReadItems(reader, cancellationToken);

        return new ContentLocksData(oldLockCount, users, items) { Now = now };
    }

    private static async Task<IReadOnlyList<ContentLockUserRow>> ReadUsers(DbDataReader reader, CancellationToken cancellationToken)
    {
        int idOrdinal = reader.GetOrdinal("UserID");
        int nameOrdinal = reader.GetOrdinal("UserName");
        int countOrdinal = reader.GetOrdinal("LockCount");
        int oldestOrdinal = reader.GetOrdinal("OldestLockedWhen");

        var rows = new List<ContentLockUserRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                reader.IsDBNull(idOrdinal) ? null : reader.GetInt32(idOrdinal),
                reader.IsDBNull(nameOrdinal) ? null : reader.GetString(nameOrdinal),
                reader.GetInt32(countOrdinal),
                reader.GetDateTime(oldestOrdinal)));
        }

        return rows;
    }

    private static async Task<(IReadOnlyList<ContentLockRow> Items, int OldLockCount)> ReadItems(DbDataReader reader, CancellationToken cancellationToken)
    {
        int idOrdinal = reader.GetOrdinal("RowID");
        int nameOrdinal = reader.GetOrdinal("DisplayName");
        int typeOrdinal = reader.GetOrdinal("ClassDisplayName");
        int languageOrdinal = reader.GetOrdinal("ContentLanguageDisplayName");
        int channelOrdinal = reader.GetOrdinal("ChannelDisplayName");
        int reusableOrdinal = reader.GetOrdinal("IsReusable");
        int workspaceOrdinal = reader.GetOrdinal("WorkspaceDisplayName");
        int userIdOrdinal = reader.GetOrdinal("UserID");
        int userNameOrdinal = reader.GetOrdinal("UserName");
        int lockedOrdinal = reader.GetOrdinal("LockedWhen");
        int modifiedOrdinal = reader.GetOrdinal("ModifiedWhen");
        int oldOrdinal = reader.GetOrdinal("OldLockCount");
        var links = StatsContentLinkReader.From(reader, withChannels: true);

        var rows = new List<ContentLockRow>();
        int oldLockCount = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                reader.GetInt32(idOrdinal),
                reader.GetString(nameOrdinal),
                reader.IsDBNull(typeOrdinal) ? string.Empty : reader.GetString(typeOrdinal),
                reader.GetString(languageOrdinal),
                reader.IsDBNull(userIdOrdinal) ? null : reader.GetInt32(userIdOrdinal),
                reader.IsDBNull(userNameOrdinal) ? null : reader.GetString(userNameOrdinal),
                reader.GetDateTime(lockedOrdinal),
                reader.GetDateTime(modifiedOrdinal))
            {
                Link = links.Read(reader),
                Channel = reader.IsDBNull(channelOrdinal) ? null : reader.GetString(channelOrdinal),
                IsReusable = !reader.IsDBNull(reusableOrdinal) && reader.GetBoolean(reusableOrdinal),
                Workspace = reader.IsDBNull(workspaceOrdinal) ? null : reader.GetString(workspaceOrdinal),
            });
            oldLockCount = reader.GetInt32(oldOrdinal);
        }

        return (rows, oldLockCount);
    }
}

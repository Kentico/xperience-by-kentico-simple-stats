using System.Data;
using System.Data.Common;

using CMS.ContentEngine;
using CMS.DataEngine;

using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContentInventory;

/// <summary>
/// Reads aggregated content counts and short lists from the database.
/// </summary>
internal interface IContentInventoryRepository
{
    /// <summary>
    /// Returns content types, languages, statuses, ages, workflow and unused reusable items for the query.
    /// </summary>
    /// <param name="query">Normalized filter.</param>
    /// <param name="now">Server time ages are counted to (<c>ModifiedWhen</c> is compared as stored).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<ContentInventoryData> GetData(StatsSnapshotQuery query, DateTime now, CancellationToken cancellationToken);
}

internal sealed class ContentInventoryRepository : IContentInventoryRepository
{
    public async Task<ContentInventoryData> GetData(StatsSnapshotQuery query, DateTime now, CancellationToken cancellationToken)
    {
        var parameters = new QueryDataParameters
        {
            new DataParameter(ContentInventorySql.ClassTypeParameter, ClassType.CONTENT_TYPE),
            new DataParameter(ContentInventorySql.OverdueBeforeParameter, now.AddDays(-ContentInventoryReportBuilder.OverdueDays)),
            new DataParameter(ContentInventorySql.LimitParameter, ContentInventoryReportBuilder.ListLimit),
            new DataParameter(ContentInventorySql.PublishedStatusParameter, (int)VersionStatus.Published),
            new DataParameter(ContentInventorySql.IncludeUnusedParameter, ContentInventoryReportBuilder.IncludesReusable(query)),
            new DataParameter(ContentInventorySql.ReusableKindParameter, ClassContentTypeType.REUSABLE),
        };
        foreach (var parameter in ContentInventorySql.GetAgeParameters(now))
        {
            parameters.Add(parameter);
        }
        if (query.Kind is string kind)
        {
            parameters.Add(new DataParameter(ContentInventorySql.KindParameter, kind));
        }
        if (query.ChannelId is int channelId)
        {
            parameters.Add(new DataParameter(ContentInventorySql.ChannelParameter, channelId));
        }

        string sql = ContentInventorySql.Build(query.Kind is not null, query.ChannelId is not null);

        await using var reader = await ConnectionHelper.ExecuteReaderAsync(sql, parameters, QueryTypeEnum.SQLQuery, CommandBehavior.Default, cancellationToken);

        var contentTypes = await ReadContentTypes(reader, cancellationToken);
        await NextResult(reader, cancellationToken);
        var languages = await ReadLanguages(reader, cancellationToken);
        await NextResult(reader, cancellationToken);
        var statuses = await ReadStatuses(reader, cancellationToken);
        await NextResult(reader, cancellationToken);
        var age = await ReadAge(reader, cancellationToken);
        await NextResult(reader, cancellationToken);
        var (oldest, _, _) = await ReadVariants(reader, VariantList.Oldest, cancellationToken);
        await NextResult(reader, cancellationToken);
        var (workflowItems, overdue, _) = await ReadVariants(reader, VariantList.Workflow, cancellationToken);
        await NextResult(reader, cancellationToken);
        var unusedByType = await ReadUnusedByType(reader, cancellationToken);
        await NextResult(reader, cancellationToken);
        var unusedItems = await ReadUnusedItems(reader, cancellationToken);
        await NextResult(reader, cancellationToken);
        var (pendingDrafts, forgotten, pending) = await ReadVariants(reader, VariantList.PendingDrafts, cancellationToken);

        return new(contentTypes, languages, statuses)
        {
            Now = now,
            Age = age,
            Oldest = oldest,
            WorkflowItems = workflowItems,
            WorkflowOverdue = overdue,
            UnusedByContentType = unusedByType,
            UnusedItems = unusedItems,
            PendingDrafts = pendingDrafts,
            PendingDraftCount = pending,
            ForgottenEditCount = forgotten,
        };
    }

    private static async Task NextResult(DbDataReader reader, CancellationToken cancellationToken)
    {
        if (!await reader.NextResultAsync(cancellationToken))
        {
            throw new InvalidOperationException("The content inventory query returned fewer result sets than expected.");
        }
    }

    private static async Task<IReadOnlyList<ContentTypeRow>> ReadContentTypes(DbDataReader reader, CancellationToken cancellationToken)
    {
        int idOrdinal = reader.GetOrdinal("ClassID");
        int nameOrdinal = reader.GetOrdinal("ClassName");
        int displayNameOrdinal = reader.GetOrdinal("ClassDisplayName");
        int kindOrdinal = reader.GetOrdinal("ClassContentTypeType");
        int countOrdinal = reader.GetOrdinal("ItemCount");

        var rows = new List<ContentTypeRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                reader.GetInt32(idOrdinal),
                reader.GetString(nameOrdinal),
                reader.IsDBNull(displayNameOrdinal) ? string.Empty : reader.GetString(displayNameOrdinal),
                reader.GetString(kindOrdinal),
                reader.GetInt32(countOrdinal)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<ContentLanguageRow>> ReadLanguages(DbDataReader reader, CancellationToken cancellationToken)
    {
        int idOrdinal = reader.GetOrdinal("ContentLanguageID");
        int nameOrdinal = reader.GetOrdinal("ContentLanguageName");
        int displayNameOrdinal = reader.GetOrdinal("ContentLanguageDisplayName");
        int defaultOrdinal = reader.GetOrdinal("ContentLanguageIsDefault");
        int countOrdinal = reader.GetOrdinal("ItemCount");

        var rows = new List<ContentLanguageRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                reader.GetInt32(idOrdinal),
                reader.GetString(nameOrdinal),
                reader.GetString(displayNameOrdinal),
                reader.GetBoolean(defaultOrdinal),
                reader.GetInt32(countOrdinal)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<ContentStatusRow>> ReadStatuses(DbDataReader reader, CancellationToken cancellationToken)
    {
        int statusOrdinal = reader.GetOrdinal("VersionStatus");
        int stepOrdinal = reader.GetOrdinal("StepID");
        int stepNameOrdinal = reader.GetOrdinal("StepDisplayName");
        int workflowOrdinal = reader.GetOrdinal("WorkflowID");
        int workflowNameOrdinal = reader.GetOrdinal("WorkflowDisplayName");
        int countOrdinal = reader.GetOrdinal("VariantCount");
        int publishOrdinal = reader.GetOrdinal("ScheduledPublish");
        int unpublishOrdinal = reader.GetOrdinal("ScheduledUnpublish");

        var rows = new List<ContentStatusRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                reader.GetInt32(statusOrdinal),
                reader.IsDBNull(stepOrdinal) ? null : reader.GetInt32(stepOrdinal),
                reader.IsDBNull(stepNameOrdinal) ? null : reader.GetString(stepNameOrdinal),
                reader.IsDBNull(workflowOrdinal) ? null : reader.GetInt32(workflowOrdinal),
                reader.IsDBNull(workflowNameOrdinal) ? null : reader.GetString(workflowNameOrdinal),
                reader.GetInt32(countOrdinal),
                reader.GetInt32(publishOrdinal),
                reader.GetInt32(unpublishOrdinal)));
        }

        return rows;
    }

    private static async Task<ContentAgeRow> ReadAge(DbDataReader reader, CancellationToken cancellationToken)
    {
        if (!await reader.ReadAsync(cancellationToken))
        {
            return ContentAgeRow.Empty;
        }

        return ContentAgeRow.Read(reader);
    }

    /// <summary>
    /// Variant lists of the batch, by the columns they have.
    /// </summary>
    private enum VariantList
    {
        /// <summary>Base columns only.</summary>
        Oldest,

        /// <summary>Also step, workflow and overdue count.</summary>
        Workflow,

        /// <summary>Also step, workflow, overdue count, pending count, channel label and last publish.</summary>
        PendingDrafts,
    }

    /// <summary>
    /// Reads a variant list with the columns of <paramref name="list"/>. Overdue and total are window counts (0 for lists without them).
    /// </summary>
    private static async Task<(IReadOnlyList<ContentVariantRow> Rows, int Overdue, int Total)> ReadVariants(
        DbDataReader reader,
        VariantList list,
        CancellationToken cancellationToken)
    {
        bool withWorkflow = list is VariantList.Workflow or VariantList.PendingDrafts;
        bool withDraft = list is VariantList.PendingDrafts;

        int idOrdinal = reader.GetOrdinal("VariantID");
        int nameOrdinal = reader.GetOrdinal("DisplayName");
        int typeOrdinal = reader.GetOrdinal("ClassDisplayName");
        int languageOrdinal = reader.GetOrdinal("ContentLanguageDisplayName");
        int modifiedOrdinal = reader.GetOrdinal("ModifiedWhen");
        int stepOrdinal = withWorkflow ? reader.GetOrdinal("StepDisplayName") : -1;
        int workflowOrdinal = withWorkflow ? reader.GetOrdinal("WorkflowID") : -1;
        int workflowNameOrdinal = withWorkflow ? reader.GetOrdinal("WorkflowDisplayName") : -1;
        int overdueOrdinal = withWorkflow ? reader.GetOrdinal("OverdueCount") : -1;
        int pendingOrdinal = withDraft ? reader.GetOrdinal("PendingCount") : -1;
        int liveOrdinal = withDraft ? reader.GetOrdinal("LivePublishedWhen") : -1;
        int channelOrdinal = withDraft ? reader.GetOrdinal("ChannelDisplayName") : -1;
        int reusableOrdinal = withDraft ? reader.GetOrdinal("IsReusable") : -1;
        int workspaceOrdinal = withDraft ? reader.GetOrdinal("WorkspaceDisplayName") : -1;
        var links = StatsContentLinkReader.From(reader, withChannels: true);

        var rows = new List<ContentVariantRow>();
        int overdue = 0;
        int total = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new ContentVariantRow(
                reader.GetInt32(idOrdinal),
                reader.GetString(nameOrdinal),
                reader.IsDBNull(typeOrdinal) ? string.Empty : reader.GetString(typeOrdinal),
                reader.GetString(languageOrdinal),
                reader.GetDateTime(modifiedOrdinal))
            {
                Link = links.Read(reader),
            };

            if (withWorkflow)
            {
                row = row with
                {
                    StepDisplayName = reader.IsDBNull(stepOrdinal) ? null : reader.GetString(stepOrdinal),
                    WorkflowId = reader.IsDBNull(workflowOrdinal) ? null : reader.GetInt32(workflowOrdinal),
                    WorkflowDisplayName = reader.IsDBNull(workflowNameOrdinal) ? null : reader.GetString(workflowNameOrdinal),
                };

                // Same value on every row.
                overdue = reader.GetInt32(overdueOrdinal);
            }

            if (withDraft)
            {
                row = row with
                {
                    LivePublishedWhen = reader.IsDBNull(liveOrdinal) ? null : reader.GetDateTime(liveOrdinal),
                    Channel = reader.IsDBNull(channelOrdinal) ? null : reader.GetString(channelOrdinal),
                    IsReusable = !reader.IsDBNull(reusableOrdinal) && reader.GetBoolean(reusableOrdinal),
                    Workspace = reader.IsDBNull(workspaceOrdinal) ? null : reader.GetString(workspaceOrdinal),
                };

                // Same value on every row.
                total = reader.GetInt32(pendingOrdinal);
            }

            rows.Add(row);
        }

        return (rows, overdue, total);
    }

    private static async Task<IReadOnlyList<ContentTypeRow>> ReadUnusedByType(DbDataReader reader, CancellationToken cancellationToken)
    {
        int idOrdinal = reader.GetOrdinal("ClassID");
        int nameOrdinal = reader.GetOrdinal("ClassName");
        int displayNameOrdinal = reader.GetOrdinal("ClassDisplayName");
        int countOrdinal = reader.GetOrdinal("ItemCount");

        var rows = new List<ContentTypeRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                reader.GetInt32(idOrdinal),
                reader.GetString(nameOrdinal),
                reader.IsDBNull(displayNameOrdinal) ? string.Empty : reader.GetString(displayNameOrdinal),
                ClassContentTypeType.REUSABLE,
                reader.GetInt32(countOrdinal)));
        }

        return rows;
    }

    private static async Task<IReadOnlyList<UnusedItemRow>> ReadUnusedItems(DbDataReader reader, CancellationToken cancellationToken)
    {
        int idOrdinal = reader.GetOrdinal("ContentItemID");
        int nameOrdinal = reader.GetOrdinal("DisplayName");
        int typeOrdinal = reader.GetOrdinal("ClassDisplayName");
        int modifiedOrdinal = reader.GetOrdinal("ModifiedWhen");

        var links = StatsContentLinkReader.From(reader, withChannels: false);

        var rows = new List<UnusedItemRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                reader.GetInt32(idOrdinal),
                reader.GetString(nameOrdinal),
                reader.IsDBNull(typeOrdinal) ? string.Empty : reader.GetString(typeOrdinal),
                reader.IsDBNull(modifiedOrdinal) ? null : reader.GetDateTime(modifiedOrdinal))
            {
                Link = links.Read(reader),
            });
        }

        return rows;
    }
}

using System.Data;
using System.Data.Common;

using CMS.DataEngine;

using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Commerce;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.OrdersRevenue;

/// <summary>
/// Reads aggregated digital commerce data from the database.
/// </summary>
internal interface IOrdersRevenueRepository
{
    /// <summary>
    /// Returns daily orders and revenue from <paramref name="previousFrom"/> to <paramref name="to"/>, items sold, orders per status (range, no status filter)
    /// and the top products (with <paramref name="orderStatusId"/> applied). <see cref="OrdersRevenueReportData.Unavailable"/> when the commerce tables do not exist.
    /// </summary>
    /// <param name="previousFrom">First day of the previous period (inclusive).</param>
    /// <param name="from">First day of the range (inclusive).</param>
    /// <param name="to">Last day of the range (inclusive).</param>
    /// <param name="orderStatusId">Optional order status ID.</param>
    /// <param name="limit">Maximum number of top products.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<OrdersRevenueReportData> GetData(DateOnly previousFrom, DateOnly from, DateOnly to, int? orderStatusId, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the order statuses in status order. Empty when the commerce tables do not exist.
    /// </summary>
    public Task<IReadOnlyList<CommerceOrderStatusOption>> GetStatuses(CancellationToken cancellationToken);
}

internal sealed class OrdersRevenueRepository : IOrdersRevenueRepository
{
    private const string ReportName = "orders and revenue";

    public async Task<OrdersRevenueReportData> GetData(DateOnly previousFrom, DateOnly from, DateOnly to, int? orderStatusId, int limit, CancellationToken cancellationToken)
    {
        var parameters = new QueryDataParameters
        {
            new DataParameter(OrdersRevenueSql.PreviousFromParameter, previousFrom.ToDateTime(TimeOnly.MinValue)),
            new DataParameter(OrdersRevenueSql.FromParameter, from.ToDateTime(TimeOnly.MinValue)),
            new DataParameter(OrdersRevenueSql.ToExclusiveParameter, to.AddDays(1).ToDateTime(TimeOnly.MinValue)),
            new DataParameter(OrdersRevenueSql.LimitParameter, limit),
        };
        if (orderStatusId is int statusId)
        {
            parameters.Add(new DataParameter(OrdersRevenueSql.OrderStatusIdParameter, statusId));
        }

        string sql = OrdersRevenueSql.BuildReport(orderStatusId is not null);

        await using var reader = await ConnectionHelper.ExecuteReaderAsync(sql, parameters, QueryTypeEnum.SQLQuery, CommandBehavior.Default, cancellationToken);

        if (!await CommerceSql.IsAvailable(reader, cancellationToken))
        {
            return OrdersRevenueReportData.Unavailable;
        }

        await CommerceSql.NextResult(reader, ReportName, cancellationToken);
        var daily = await ReadDaily(reader, cancellationToken);
        await CommerceSql.NextResult(reader, ReportName, cancellationToken);
        var itemsSold = await ReadItemsSold(reader, cancellationToken);
        await CommerceSql.NextResult(reader, ReportName, cancellationToken);
        var byStatus = await ReadByStatus(reader, cancellationToken);
        await CommerceSql.NextResult(reader, ReportName, cancellationToken);
        var (products, productCount, productRevenue) = await ReadProducts(reader, cancellationToken);

        return new(true, daily, itemsSold, byStatus, products, productCount, productRevenue);
    }

    public Task<IReadOnlyList<CommerceOrderStatusOption>> GetStatuses(CancellationToken cancellationToken) =>
        CommerceOrderStatuses.Read(cancellationToken);

    private static async Task<IReadOnlyList<OrdersRevenueDailyRow>> ReadDaily(DbDataReader reader, CancellationToken cancellationToken)
    {
        int dateOrdinal = reader.GetOrdinal(OrdersRevenueSql.DateColumn);
        int ordersOrdinal = reader.GetOrdinal(OrdersRevenueSql.OrdersColumn);
        int revenueOrdinal = reader.GetOrdinal(OrdersRevenueSql.RevenueColumn);

        var rows = new List<OrdersRevenueDailyRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                DateOnly.FromDateTime(reader.GetDateTime(dateOrdinal)),
                reader.GetInt32(ordersOrdinal),
                reader.GetDecimal(revenueOrdinal)));
        }

        return rows;
    }

    private static async Task<(decimal Current, decimal Previous)> ReadItemsSold(DbDataReader reader, CancellationToken cancellationToken)
    {
        if (!await reader.ReadAsync(cancellationToken))
        {
            return (0, 0);
        }

        return (
            reader.GetDecimal(reader.GetOrdinal(OrdersRevenueSql.CurrentQuantityColumn)),
            reader.GetDecimal(reader.GetOrdinal(OrdersRevenueSql.PreviousQuantityColumn)));
    }

    private static async Task<IReadOnlyList<OrdersRevenueStatusRow>> ReadByStatus(DbDataReader reader, CancellationToken cancellationToken)
    {
        int idOrdinal = reader.GetOrdinal(OrdersRevenueSql.StatusIdColumn);
        int codeNameOrdinal = reader.GetOrdinal(OrdersRevenueSql.StatusCodeNameColumn);
        int nameOrdinal = reader.GetOrdinal(OrdersRevenueSql.StatusNameColumn);
        int ordersOrdinal = reader.GetOrdinal(OrdersRevenueSql.OrdersColumn);
        int revenueOrdinal = reader.GetOrdinal(OrdersRevenueSql.RevenueColumn);

        var rows = new List<OrdersRevenueStatusRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                reader.GetInt32(idOrdinal),
                reader.GetString(codeNameOrdinal),
                reader.GetString(nameOrdinal),
                reader.GetInt32(ordersOrdinal),
                reader.GetDecimal(revenueOrdinal)));
        }

        return rows;
    }

    private static async Task<(IReadOnlyList<OrdersRevenueProductRow> Rows, int GroupCount, decimal TotalRevenue)> ReadProducts(
        DbDataReader reader,
        CancellationToken cancellationToken)
    {
        int keyOrdinal = reader.GetOrdinal(OrdersRevenueSql.ProductKeyColumn);
        int skuOrdinal = reader.GetOrdinal(OrdersRevenueSql.SkuColumn);
        int nameOrdinal = reader.GetOrdinal(OrdersRevenueSql.NameColumn);
        int revenueOrdinal = reader.GetOrdinal(OrdersRevenueSql.RevenueColumn);
        int quantityOrdinal = reader.GetOrdinal(OrdersRevenueSql.QuantityColumn);
        int previousOrdinal = reader.GetOrdinal(OrdersRevenueSql.PreviousRevenueColumn);
        int groupCountOrdinal = reader.GetOrdinal(OrdersRevenueSql.GroupCountColumn);
        int totalOrdinal = reader.GetOrdinal(OrdersRevenueSql.TotalRevenueColumn);

        var rows = new List<OrdersRevenueProductRow>();
        int groupCount = 0;
        decimal totalRevenue = 0;
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new(
                reader.GetString(keyOrdinal),
                reader.IsDBNull(skuOrdinal) ? null : reader.GetString(skuOrdinal),
                reader.IsDBNull(nameOrdinal) ? null : reader.GetString(nameOrdinal),
                reader.GetDecimal(revenueOrdinal),
                reader.GetDecimal(quantityOrdinal),
                reader.GetDecimal(previousOrdinal)));

            // Same values on every row.
            groupCount = reader.GetInt32(groupCountOrdinal);
            totalRevenue = reader.GetDecimal(totalOrdinal);
        }

        return (rows, groupCount, totalRevenue);
    }
}

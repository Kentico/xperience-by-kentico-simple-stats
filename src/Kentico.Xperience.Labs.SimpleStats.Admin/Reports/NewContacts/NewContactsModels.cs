using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.NewContacts;

/// <summary>
/// New contacts over time, split into identified and anonymous contacts.
/// </summary>
/// <param name="Trend">
/// Contacts per period. Series <c>identified</c> and <c>anonymous</c>, always both and in this order.
/// </param>
/// <param name="Identified">New contacts with an email address in the range.</param>
/// <param name="Anonymous">New contacts without an email address in the range.</param>
/// <param name="IdentifiedShare">Share of identified contacts (0-1). 0 when there are no new contacts.</param>
/// <param name="TotalComparison">New contacts (identified + anonymous) compared with the previous period of the same length.</param>
/// <param name="IdentifiedComparison"><paramref name="Identified"/> compared with the previous period of the same length.</param>
/// <param name="AnonymousComparison"><paramref name="Anonymous"/> compared with the previous period of the same length.</param>
public sealed record NewContactsResult(
    StatsTimeSeriesResult Trend,
    int Identified,
    int Anonymous,
    double IdentifiedShare,
    StatsComparison TotalComparison,
    StatsComparison IdentifiedComparison,
    StatsComparison AnonymousComparison);

/// <summary>
/// Contacts created on one day, as returned by the SQL aggregate.
/// </summary>
/// <param name="Date">Day the contacts were created.</param>
/// <param name="Identified">Contacts with an email address.</param>
/// <param name="Anonymous">Contacts without an email address.</param>
public sealed record NewContactsDailyCount(DateOnly Date, int Identified, int Anonymous);

/// <summary>
/// Daily counts for one range, with the time they were read. This is the cached value.
/// </summary>
internal sealed record NewContactsSnapshot(IReadOnlyList<NewContactsDailyCount> Rows, DateTimeOffset ReadAt);

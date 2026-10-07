using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Commerce;

/// <summary>
/// Guesses the <see cref="StatsTone"/> of an order status from its names, for chart colors.
/// </summary>
/// <remarks>
/// Order statuses are defined by each project and have no color or meaning in the database, so this is only a
/// name-based guess: a case-insensitive keyword match on the code name and the display name.
/// <see cref="ProblemKeywords"/> are checked first, then <see cref="CautionKeywords"/>, then <see cref="DoneKeywords"/>,
/// so "Payment failed" is a problem and "Unpaid" is caution (not done because of "paid"). No match is <see cref="StatsTone.Neutral"/>.
/// </remarks>
internal static class OrderStatusTones
{
    /// <summary>Keywords of statuses where something went wrong.</summary>
    public static IReadOnlyList<string> ProblemKeywords { get; } =
        ["fail", "cancel", "refund", "decline", "reject", "return", "void", "chargeback", "error"];

    /// <summary>Keywords of statuses that wait for something.</summary>
    public static IReadOnlyList<string> CautionKeywords { get; } =
        ["pending", "hold", "await", "wait", "new", "review", "backorder", "unpaid"];

    /// <summary>Keywords of finished or good statuses. "fulfil" covers fulfilled, fulfillment and fulfilment.</summary>
    public static IReadOnlyList<string> DoneKeywords { get; } =
        ["fulfil", "complete", "deliver", "ship", "paid", "received", "success"];

    /// <summary>
    /// Returns the guessed tone of a status.
    /// </summary>
    /// <param name="codeName">Status code name (<c>OrderStatusName</c>), for example <c>PaymentFailed</c>.</param>
    /// <param name="displayName">Status display name, for example <c>Payment failed</c>.</param>
    public static StatsTone Get(string? codeName, string? displayName)
    {
        string text = $"{codeName} {displayName}";

        if (Matches(text, ProblemKeywords))
        {
            return StatsTone.Problem;
        }

        if (Matches(text, CautionKeywords))
        {
            return StatsTone.Caution;
        }

        return Matches(text, DoneKeywords) ? StatsTone.Done : StatsTone.Neutral;
    }

    private static bool Matches(string text, IReadOnlyList<string> keywords) =>
        keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
}

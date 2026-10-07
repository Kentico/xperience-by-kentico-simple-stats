using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class StatsSnapshotTests
{
    private static readonly string[] kinds = ["Website", "Reusable"];

    [Test]
    public void Normalize_Defaults_AreAll()
    {
        var query = new StatsSnapshotFilter().Normalize(kinds);

        Assert.That(query, Is.EqualTo(new StatsSnapshotQuery(null, null)));
    }

    [TestCase("website", "Website")]
    [TestCase(" Reusable ", "Reusable")]
    [TestCase("Email", null)]
    [TestCase("", null)]
    [TestCase("'; DROP TABLE x; --", null)]
    public void Normalize_Kind_MatchesSupportedKindsOnly(string kind, string? expected)
    {
        var query = new StatsSnapshotFilter { Kind = kind }.Normalize(kinds);

        Assert.That(query.Kind, Is.EqualTo(expected));
    }

    [TestCase(3, 3)]
    [TestCase(0, null)]
    [TestCase(-1, null)]
    [TestCase(null, null)]
    public void Normalize_Channel_KeepsPositiveIdsOnly(int? channelId, int? expected)
    {
        var query = new StatsSnapshotFilter { ChannelId = channelId }.Normalize(kinds);

        Assert.That(query.ChannelId, Is.EqualTo(expected));
    }

    [Test]
    public void Normalize_Channel_DroppedWhenCheckRejectsIt()
    {
        var filter = new StatsSnapshotFilter { Kind = "website", ChannelId = 3 };

        var allowed = filter.Normalize(kinds, (kind, id) => kind == "Website" && id == 3);
        var rejected = filter.Normalize(kinds, (_, _) => false);

        Assert.That(allowed, Is.EqualTo(new StatsSnapshotQuery("Website", 3)));
        Assert.That(rejected, Is.EqualTo(new StatsSnapshotQuery("Website", null)));
    }

    [TestCase(7, 7)]
    [TestCase(90, 90)]
    [TestCase(30, 30)]
    [TestCase(14, 30)]
    [TestCase(0, 30)]
    [TestCase(-7, 30)]
    [TestCase(null, 30)]
    public void NormalizeWindow_KeepsSupportedWindowsOnly(int? window, int expected)
    {
        int normalized = new StatsSnapshotFilter { Window = window }.NormalizeWindow([7, 30, 90], 30);

        Assert.That(normalized, Is.EqualTo(expected));
    }

    [Test]
    public void Window_IsNotPartOfTheQuery()
    {
        var query = new StatsSnapshotFilter { Kind = "website", ChannelId = 3, Window = 7 }.Normalize(kinds);

        Assert.That(query, Is.EqualTo(new StatsSnapshotQuery("Website", 3)));
    }

    [TestCase(3, 4, 1, 0.75)]
    [TestCase(0, 0, 0, 0)]
    [TestCase(5, 4, 0, 1)]
    public void CoverageItem_ComputesMissingAndShare(int covered, int total, int missing, double share)
    {
        var item = new StatsCoverageItem("en", "English", null, covered, total);

        Assert.That(item.Missing, Is.EqualTo(missing));
        Assert.That(item.Share, Is.EqualTo(share).Within(1e-9));
    }
}

using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PublishingActivity;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class PublishingActivityReportBuilderTests
{
    // Oct 1–7; previous period Sep 24–30.
    private static readonly PublishingActivityQuery query = new(new(new(2026, 10, 1), new(2026, 10, 7), StatsGrouping.Day, null), null);

    private static readonly IReadOnlyList<StatsChannelOption> channels =
    [
        new(1, "Pages", "Website"),
        new(2, "Emails", "Email"),
    ];

    [Test]
    public void Build_HistoryEnabled_HasThreeSeriesAndUpdates()
    {
        var data = PublishingActivityData.Empty with
        {
            Daily =
            [
                new(PublishingActivitySeriesKeys.Created, new(2026, 10, 1), 2),
                new(PublishingActivitySeriesKeys.FirstPublished, new(2026, 10, 2), 1),
                new(PublishingActivitySeriesKeys.Updates, new(2026, 10, 7), 12),
                new(PublishingActivitySeriesKeys.Updates, new(2026, 9, 30), 3),
            ],
        };

        var result = PublishingActivityReportBuilder.Build(query, data, versionHistoryEnabled: true, versionHistoryLength: 20);

        Assert.Multiple(() =>
        {
            Assert.That(result.Series.Series.Select(s => s.Key), Is.EqualTo(new[] { "created", "published", "updates" }));
            Assert.That(result.Series.Periods, Has.Count.EqualTo(7));
            Assert.That(result.Updates?.Current, Is.EqualTo(12));
            Assert.That(result.Updates?.Previous, Is.EqualTo(3));
            Assert.That(result.VersionHistoryEnabled, Is.True);
            Assert.That(result.VersionHistoryLength, Is.EqualTo(20));
        });
    }

    [Test]
    public void Build_HistoryDisabled_HasNoUpdatesSeriesOrKpi()
    {
        var data = PublishingActivityData.Empty with
        {
            Daily = [new(PublishingActivitySeriesKeys.Updates, new(2026, 10, 7), 12)],
            ContentTypes = [new(5, "Article", 0, 0, 4, null)],
        };

        var result = PublishingActivityReportBuilder.Build(query, data, versionHistoryEnabled: false, versionHistoryLength: 20);

        Assert.Multiple(() =>
        {
            Assert.That(result.Series.Series.Select(s => s.Key), Is.EqualTo(new[] { "created", "published" }));
            Assert.That(result.Updates, Is.Null);
            Assert.That(result.VersionHistoryEnabled, Is.False);
            // A type with updates only has no activity without version history.
            Assert.That(result.ByContentType, Is.Empty);
        });
    }

    [Test]
    public void Build_ComparesWithPreviousPeriod_RangeEdgesIncluded()
    {
        var data = PublishingActivityData.Empty with
        {
            Daily =
            [
                new(PublishingActivitySeriesKeys.Created, new(2026, 10, 1), 1),
                new(PublishingActivitySeriesKeys.Created, new(2026, 10, 7), 3),
                new(PublishingActivitySeriesKeys.Created, new(2026, 9, 24), 2),
                new(PublishingActivitySeriesKeys.Created, new(2026, 9, 30), 2),
                // Outside both periods: ignored.
                new(PublishingActivitySeriesKeys.Created, new(2026, 9, 23), 50),
                new(PublishingActivitySeriesKeys.Created, new(2026, 10, 8), 50),
            ],
        };

        var result = PublishingActivityReportBuilder.Build(query, data, versionHistoryEnabled: true, versionHistoryLength: 20);

        Assert.Multiple(() =>
        {
            Assert.That(result.Created.Current, Is.EqualTo(4));
            Assert.That(result.Created.Previous, Is.EqualTo(4));
            Assert.That(result.Created.Change, Is.EqualTo(0));
            Assert.That(result.Created.PreviousFrom, Is.EqualTo(new DateOnly(2026, 9, 24)));
            Assert.That(result.Series.Series[0].Total, Is.EqualTo(4));
        });
    }

    [Test]
    public void Build_PublishedDateUnknown_NotInSeries()
    {
        var data = PublishingActivityData.Empty with { Totals = new(29, null, null) };

        var result = PublishingActivityReportBuilder.Build(query, data, versionHistoryEnabled: true, versionHistoryLength: 20);

        Assert.Multiple(() =>
        {
            Assert.That(result.PublishedDateUnknown, Is.EqualTo(29));
            Assert.That(result.FirstPublished.Current, Is.Zero);
            Assert.That(result.Series.Total, Is.Zero);
            Assert.That(result.MedianDaysToPublish, Is.Null);
        });
    }

    [Test]
    public void Build_RoundsDays_AndClampsNegative()
    {
        var data = PublishingActivityData.Empty with
        {
            Totals = new(0, 5.0708, 955.62),
            ContentTypes = [new(5, "Article", 1, 1, 0, -2)],
        };

        var result = PublishingActivityReportBuilder.Build(query, data, versionHistoryEnabled: true, versionHistoryLength: -1);

        Assert.Multiple(() =>
        {
            Assert.That(result.MedianDaysToPublish, Is.EqualTo(5.1m));
            Assert.That(result.Percentile90DaysToPublish, Is.EqualTo(955.6m));
            Assert.That(result.ByContentType.Single().MedianDaysToPublish, Is.Zero);
            Assert.That(result.VersionHistoryLength, Is.Zero);
        });
    }

    [Test]
    public void Build_ContentTypes_MostActivityFirst_WithLinks()
    {
        var data = PublishingActivityData.Empty with
        {
            ContentTypes =
            [
                new(5, "Article", 1, 0, 0, null),
                new(6, "Email", 2, 2, 3, 1.25),
                new(7, "Unused", 0, 0, 0, null),
            ],
        };

        var result = PublishingActivityReportBuilder.Build(
            query, data, versionHistoryEnabled: true, versionHistoryLength: 20, getContentTypePath: id => $"/types/{id}");

        Assert.Multiple(() =>
        {
            Assert.That(result.ByContentType.Select(t => t.ContentType), Is.EqualTo(new[] { "Email", "Article" }));
            Assert.That(result.ByContentType[0].Updates, Is.EqualTo(3));
            Assert.That(result.ByContentType[0].MedianDaysToPublish, Is.EqualTo(1.3m));
            Assert.That(result.ByContentType[0].AdminPath, Is.EqualTo("/types/6"));
            Assert.That(result.ByContentType[0].Key, Is.EqualTo("type:6"));
        });
    }

    [Test]
    public void Build_Slowest_LeavesOutFirstPublishBeforeCreated_AndLinks()
    {
        var link = new ContentItemLink(ContentItemLocation.WebPage, 1, 14, "en");
        var data = PublishingActivityData.Empty with
        {
            Slowest =
            [
                new(1, "Slow page", "Article", "English", new(2026, 1, 1, 8, 0, 0, DateTimeKind.Unspecified), new(2026, 10, 2, 9, 0, 0, DateTimeKind.Unspecified))
                {
                    Link = link,
                    Channel = "Pages",
                },
                new(2, "Broken", "Article", "English", new(2026, 10, 3, 0, 0, 0, DateTimeKind.Unspecified), new(2026, 10, 2, 0, 0, 0, DateTimeKind.Unspecified)),
            ],
        };

        var result = PublishingActivityReportBuilder.Build(
            query, data, versionHistoryEnabled: true, versionHistoryLength: 20, getContentItemPath: l => $"/item/{l.ObjectId}");

        var item = result.Slowest.Single();
        Assert.Multiple(() =>
        {
            Assert.That(item.Label, Is.EqualTo("Slow page"));
            Assert.That(item.Since, Is.EqualTo(new DateOnly(2026, 1, 1)));
            Assert.That(item.Until, Is.EqualTo(new DateOnly(2026, 10, 2)));
            Assert.That(item.Days, Is.EqualTo(274));
            Assert.That(item.AdminPath, Is.EqualTo("/item/14"));
            Assert.That(item.Channel, Is.EqualTo("Pages"));
            Assert.That(item.Category, Is.EqualTo("Article"));
        });
    }

    [Test]
    public void Filter_Normalize_KeepsChannelOnlyWithMatchingKind()
    {
        var today = new DateOnly(2026, 10, 7);

        var pages = new PublishingActivityFilter { Kind = "website", Range = new StatsFilter { ChannelId = 1 } }.Normalize(today, channels);
        var wrongType = new PublishingActivityFilter { Kind = "Website", Range = new StatsFilter { ChannelId = 2 } }.Normalize(today, channels);
        var allKinds = new PublishingActivityFilter { Range = new StatsFilter { ChannelId = 1 } }.Normalize(today, channels);
        var unknown = new PublishingActivityFilter { Kind = "Folder" }.Normalize(today, channels);

        Assert.Multiple(() =>
        {
            Assert.That(pages.Kind, Is.EqualTo("Website"));
            Assert.That(pages.Range.ChannelId, Is.EqualTo(1));
            Assert.That(wrongType.Range.ChannelId, Is.Null);
            Assert.That(allKinds.Range.ChannelId, Is.Null);
            Assert.That(unknown.Kind, Is.Null);
            Assert.That(unknown.Range.To, Is.EqualTo(today));
            Assert.That(unknown.Range.From, Is.EqualTo(today.AddDays(-(StatsFilter.DefaultRangeDays - 1))));
        });
    }
}

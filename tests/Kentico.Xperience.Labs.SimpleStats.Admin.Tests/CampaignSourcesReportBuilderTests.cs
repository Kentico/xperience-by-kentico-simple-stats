using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.CampaignSources;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class CampaignSourcesReportBuilderTests
{
    private static readonly StatsQuery range = new(new(2026, 9, 1), new(2026, 9, 7), StatsGrouping.Day, null);
    private static readonly CampaignSourcesQuery query = new(range, null, null);
    private static readonly StatsChannelOption[] websiteChannels = [new(2, "Pages", "Website")];

    [Test]
    public void Build_NoData_ReturnsEmptyReport()
    {
        var result = CampaignSourcesReportBuilder.Build(query, CampaignSourcesData.Empty, hasAnyUtmData: false);

        Assert.Multiple(() =>
        {
            Assert.That(result.Landings.Current, Is.Zero);
            Assert.That(result.CampaignLandings.Current, Is.Zero);
            Assert.That(result.CampaignShare, Is.Null);
            Assert.That(result.Series.Series, Is.Empty);
            Assert.That(result.Series.Periods, Has.Count.EqualTo(7));
            Assert.That(result.BySource.Items, Is.Empty);
            Assert.That(result.Pages.Items, Is.Empty);
            Assert.That(result.BySourceContent.Items, Is.Empty);
            Assert.That(result.SourceOptions, Is.Empty);
            Assert.That(result.ContentOptions, Is.Empty);
            Assert.That(result.HasAnyUtmData, Is.False);
        });
    }

    [Test]
    public void Build_Totals_CompareWithPreviousPeriod()
    {
        var data = CampaignSourcesData.Empty with
        {
            Totals = new(Landings: 20, PreviousLandings: 10, AllCampaignLandings: 8, CampaignLandings: 8, PreviousCampaignLandings: 4, CampaignVisitors: 6, Sources: 3, PreviousSources: 0),
        };

        var result = CampaignSourcesReportBuilder.Build(query, data, hasAnyUtmData: false);

        Assert.Multiple(() =>
        {
            Assert.That(result.Landings.Current, Is.EqualTo(20));
            Assert.That(result.Landings.Change, Is.EqualTo(1.0).Within(1e-9));
            Assert.That(result.Landings.PreviousFrom, Is.EqualTo(new DateOnly(2026, 8, 25)));
            Assert.That(result.CampaignLandings.Change, Is.EqualTo(1.0).Within(1e-9));
            Assert.That(result.CampaignShare, Is.EqualTo(0.4).Within(1e-9));
            Assert.That(result.CampaignVisitors, Is.EqualTo(6));
            Assert.That(result.Sources.Current, Is.EqualTo(3));
            // No previous sources: no change.
            Assert.That(result.Sources.Change, Is.Null);
            // Campaign landings in the range prove UTM data exists.
            Assert.That(result.HasAnyUtmData, Is.True);
        });
    }

    [Test]
    public void Build_LandingsWithoutUtm_ShareIsZero()
    {
        var data = CampaignSourcesData.Empty with { Totals = CampaignSourcesTotalsRow.Empty with { Landings = 5 } };

        var result = CampaignSourcesReportBuilder.Build(query, data, hasAnyUtmData: true);

        Assert.Multiple(() =>
        {
            Assert.That(result.CampaignShare, Is.Zero);
            Assert.That(result.HasAnyUtmData, Is.True);
        });
    }

    [Test]
    public void Build_Sources_RankedWithPreviousPeriod()
    {
        var data = CampaignSourcesData.Empty with
        {
            Totals = CampaignSourcesTotalsRow.Empty with { AllCampaignLandings = 10, Sources = 12 },
            Sources = [new("linkedin", 3, 2, 6), new("newsletter", 7, 5, 0)],
        };

        var result = CampaignSourcesReportBuilder.Build(query, data, hasAnyUtmData: true);

        Assert.Multiple(() =>
        {
            Assert.That(result.BySource.Items.Select(i => i.Label), Is.EqualTo(new[] { "newsletter", "linkedin" }));
            Assert.That(result.BySource.Items[0].SecondaryValue, Is.EqualTo(5));
            Assert.That(result.BySource.Items[0].Share, Is.EqualTo(0.7).Within(1e-9));
            Assert.That(result.BySource.Items[0].Change, Is.Null);
            Assert.That(result.BySource.Items[1].PreviousValue, Is.EqualTo(6));
            Assert.That(result.BySource.Items[1].Change, Is.EqualTo(-0.5).Within(1e-9));
            Assert.That(result.BySource.ItemCount, Is.EqualTo(12));
            Assert.That(result.SourceOptions, Is.EqualTo(new[] { "linkedin", "newsletter" }));
        });
    }

    [Test]
    public void Build_SourceList_IsCappedButOptionsAreNot()
    {
        var sources = Enumerable.Range(1, 15).Select(i => new CampaignSourcesSourceRow($"source-{i:00}", 100 - i, 1, 0)).ToList();
        var data = CampaignSourcesData.Empty with { Sources = sources };

        var result = CampaignSourcesReportBuilder.Build(query, data, hasAnyUtmData: true);

        Assert.Multiple(() =>
        {
            Assert.That(result.BySource.Items, Has.Count.EqualTo(CampaignSourcesReportBuilder.SourceLimit));
            Assert.That(result.SourceOptions, Has.Count.EqualTo(15));
        });
    }

    [Test]
    public void Build_SelectedSourceAndContent_AreKeptInOptions()
    {
        var selected = query with { Source = "tiktok", Content = string.Empty };
        var data = CampaignSourcesData.Empty with
        {
            Sources = [new("newsletter", 3, 2, 0)],
            Contents = [new("tiktok", "video", 2, 2)],
        };

        var result = CampaignSourcesReportBuilder.Build(selected, data, hasAnyUtmData: true);

        Assert.Multiple(() =>
        {
            Assert.That(result.Source, Is.EqualTo("tiktok"));
            Assert.That(result.Content, Is.Empty);
            Assert.That(result.SourceOptions, Is.EqualTo(new[] { "newsletter", "tiktok" }));
            Assert.That(result.ContentOptions, Is.EqualTo(new[] { "video", string.Empty }));
        });
    }

    [Test]
    public void Build_Pages_UseNameChannelLanguageAndPublicUrl()
    {
        var guid = Guid.Parse("9276d053-d581-4d48-80c6-953b088ec5aa");
        var data = CampaignSourcesData.Empty with
        {
            Totals = CampaignSourcesTotalsRow.Empty with { CampaignLandings = 10 },
            Pages =
            [
                new(guid, 1, 6, 4, "https://example.com/", "Home", "English", "Pages"),
                new(guid, 2, 3, 3, "https://example.com/es/", "Inicio", "Spanish", null),
                // Deleted page: the URL is the label. A relative URL is not a link.
                new(Guid.NewGuid(), 1, 1, 1, "/old-page", null, null, null),
            ],
            PageCount = 30,
        };

        var result = CampaignSourcesReportBuilder.Build(query, data, hasAnyUtmData: true);
        var items = result.Pages.Items;

        Assert.Multiple(() =>
        {
            Assert.That(items.Select(i => i.Label), Is.EqualTo(new[] { "Home", "Inicio", "/old-page" }));
            Assert.That(items.Select(i => i.SecondaryLabel), Is.EqualTo(new[] { "Pages · English", "Spanish", null }));
            Assert.That(items.Select(i => i.Url), Is.EqualTo(new[] { "https://example.com/", "https://example.com/es/", null }));
            Assert.That(items.Select(i => i.Key), Is.Unique);
            Assert.That(items[0].SecondaryValue, Is.EqualTo(4));
            Assert.That(items[0].Share, Is.EqualTo(0.6).Within(1e-9));
            Assert.That(result.Pages.ItemCount, Is.EqualTo(30));
        });
    }

    [Test]
    public void Build_Pages_WithoutNameOrUrl_AreUnknown()
    {
        var data = CampaignSourcesData.Empty with { Pages = [new(null, null, 2, 1, null, null, null, null)] };

        var result = CampaignSourcesReportBuilder.Build(query, data, hasAnyUtmData: true);

        Assert.That(result.Pages.Items.Single().Label, Is.EqualTo(CampaignSourcesReportBuilder.UnknownPageLabel));
    }

    [Test]
    public void Build_Contents_ShowNoneForEmptyContent_TotalOfSelectedSource()
    {
        var selected = query with { Source = "newsletter" };
        var data = CampaignSourcesData.Empty with
        {
            Totals = CampaignSourcesTotalsRow.Empty with { AllCampaignLandings = 50 },
            Sources = [new("newsletter", 10, 8, 0), new("google", 40, 30, 0)],
            Contents = [new("newsletter", "hero-banner", 6, 5), new("newsletter", null, 4, 4)],
            ContentCount = 2,
        };

        var result = CampaignSourcesReportBuilder.Build(selected, data, hasAnyUtmData: true);

        Assert.Multiple(() =>
        {
            Assert.That(result.BySourceContent.Items.Select(i => (i.Label, i.SecondaryLabel, i.Value)), Is.EqualTo(new[]
            {
                ("newsletter", "hero-banner", 6m),
                ("newsletter", "(none)", 4m),
            }));
            Assert.That(result.BySourceContent.Total, Is.EqualTo(10));
            Assert.That(result.BySourceContent.Items[0].Share, Is.EqualTo(0.6).Within(1e-9));
            Assert.That(result.ContentOptions, Is.EqualTo(new[] { "hero-banner", string.Empty }));
        });
    }

    [Test]
    public void BuildSeries_TopSourcesThenOther()
    {
        var daily = new CampaignSourcesDailyRow[]
        {
            new("google", new(2026, 9, 1), 2),
            new("newsletter", new(2026, 9, 1), 3),
            new("newsletter", new(2026, 9, 2), 2),
            new(null, new(2026, 9, 2), 4),
        };

        var series = CampaignSourcesReportBuilder.BuildSeries(range, daily);

        Assert.Multiple(() =>
        {
            Assert.That(series.Series.Select(s => s.DisplayName), Is.EqualTo(new[] { "newsletter", "google", "Other" }));
            Assert.That(series.Series[0].Values.Take(2), Is.EqualTo(new[] { 3, 2 }));
            Assert.That(series.Series[2].Key, Is.EqualTo(StatsTimeSeriesBuilder.OtherSeries.Key));
            Assert.That(series.Total, Is.EqualTo(11));
        });
    }

    [Test]
    public void BuildSeries_SourceNamedLikeOther_DoesNotMergeWithOther()
    {
        var daily = new CampaignSourcesDailyRow[]
        {
            new(StatsTimeSeriesBuilder.OtherSeries.Key, new(2026, 9, 1), 1),
            new(null, new(2026, 9, 1), 2),
        };

        var series = CampaignSourcesReportBuilder.BuildSeries(range, daily);

        Assert.That(series.Series.Select(s => s.Total), Is.EqualTo(new[] { 1, 2 }));
    }

    [Test]
    public void Normalize_TrimsSource_IgnoresContentWithoutSource_DropsOtherChannels()
    {
        var today = new DateOnly(2026, 9, 30);

        var noSource = new CampaignSourcesFilter { Source = "  ", Content = "footer", Range = new StatsFilter { ChannelId = 1 } }.Normalize(today, websiteChannels);
        var withSource = new CampaignSourcesFilter { Source = " newsletter ", Content = " ", Range = new StatsFilter { ChannelId = 2 } }.Normalize(today, websiteChannels);
        var allContents = new CampaignSourcesFilter { Source = "newsletter" }.Normalize(today, websiteChannels);
        var tooLong = new CampaignSourcesFilter { Source = new string('x', 300) }.Normalize(today, websiteChannels);

        Assert.Multiple(() =>
        {
            Assert.That(noSource.Source, Is.Null);
            Assert.That(noSource.Content, Is.Null);
            Assert.That(noSource.Range.ChannelId, Is.Null);
            Assert.That(withSource.Source, Is.EqualTo("newsletter"));
            // Whitespace-only content means "no content", as stored values are trimmed.
            Assert.That(withSource.Content, Is.Empty);
            Assert.That(withSource.Range.ChannelId, Is.EqualTo(2));
            Assert.That(allContents.Content, Is.Null);
            Assert.That(tooLong.Source, Has.Length.EqualTo(CampaignSourcesReportBuilder.ValueMaxLength));
        });
    }
}

using System.Text.RegularExpressions;

using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.CampaignSources;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.WebPageStats;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class CampaignSourcesSqlTests
{
    [Test]
    public void Build_HasNoPlaceholders_AndUsesTheSharedUtmColumns()
    {
        string sql = CampaignSourcesSql.Build(hasChannel: true, hasSource: true, CampaignContentFilter.Value);

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Not.Contain("{").And.Not.Contain("}"));
            Assert.That(sql, Does.Contain(StatsUtm.SourceColumn));
            Assert.That(sql, Does.Contain(StatsUtm.ContentColumn));
            // Same trimming as the web page "Stats (Labs)" tab.
            Assert.That(WebPageStatsRepository.CampaignQuery, Does.Contain(StatsUtm.SourceColumn));
            Assert.That(sql, Does.Contain("A.[ActivityType] = " + CampaignSourcesSql.LandingPageTypeParameter));
            // Previous period start to range end, half-open.
            Assert.That(sql, Does.Contain("A.[ActivityCreated] >= " + CampaignSourcesSql.PreviousFromParameter));
            Assert.That(sql, Does.Contain("A.[ActivityCreated] < " + CampaignSourcesSql.ToExclusiveParameter));
            Assert.That(sql, Does.Contain("TOP (" + CampaignSourcesSql.SeriesLimitParameter + ")"));
            Assert.That(sql, Does.Contain("TOP (" + CampaignSourcesSql.SourceLimitParameter + ")"));
            Assert.That(sql, Does.Contain("TOP (" + CampaignSourcesSql.PageLimitParameter + ")"));
            Assert.That(sql, Does.Contain("TOP (" + CampaignSourcesSql.ContentLimitParameter + ")"));
        });
    }

    [Test]
    public void Build_WithoutFilters_HasNoFilterParameters()
    {
        string sql = CampaignSourcesSql.Build(hasChannel: false, hasSource: false, CampaignContentFilter.All);

        Assert.Multiple(() =>
        {
            Assert.That(sql, Does.Not.Contain("= " + CampaignSourcesSql.ChannelParameter));
            Assert.That(sql, Does.Not.Contain("= " + CampaignSourcesSql.SourceParameter));
            Assert.That(sql, Does.Not.Contain("= " + CampaignSourcesSql.ContentParameter));
            Assert.That(sql, Does.Not.Contain("L.[Content] IS NULL"));
        });
    }

    [Test]
    public void Build_Filters_ApplyWhereExpected()
    {
        string sql = CampaignSourcesSql.Build(hasChannel: true, hasSource: true, CampaignContentFilter.Value);

        Assert.Multiple(() =>
        {
            Assert.That(Count(sql, "A.[ActivityChannelID] = " + CampaignSourcesSql.ChannelParameter), Is.EqualTo(1));
            // Totals (3), series (2), pages (1) and the source and content list (1) follow the source.
            Assert.That(Count(sql, "L.[Source] = " + CampaignSourcesSql.SourceParameter), Is.EqualTo(7));
            // The source and content list ignores the content filter.
            Assert.That(Count(sql, "L.[Content] = " + CampaignSourcesSql.ContentParameter), Is.EqualTo(6));
        });
    }

    [Test]
    public void Build_NoContent_MatchesEmptyContent()
    {
        string sql = CampaignSourcesSql.Build(hasChannel: false, hasSource: true, CampaignContentFilter.None);

        Assert.Multiple(() =>
        {
            Assert.That(Count(sql, "L.[Content] IS NULL"), Is.EqualTo(6));
            Assert.That(sql, Does.Not.Contain("= " + CampaignSourcesSql.ContentParameter));
        });
    }

    [Test]
    public void Build_ContentWithoutSource_IsIgnored()
    {
        string sql = CampaignSourcesSql.Build(hasChannel: false, hasSource: false, CampaignContentFilter.Value);

        Assert.That(sql, Does.Not.Contain("= " + CampaignSourcesSql.ContentParameter));
    }

    [Test]
    public void Build_ReturnsOnlyAggregatesOfContacts()
    {
        // Contact IDs are copied into the table variable; the result sets may use them only inside COUNT(DISTINCT ...).
        string sql = CampaignSourcesSql.Build(hasChannel: true, hasSource: true, CampaignContentFilter.Value);
        string selects = sql[(sql.IndexOf(';', sql.IndexOf("INSERT INTO @Landings", StringComparison.Ordinal)) + 1)..];
        int references = Regex.Matches(selects, @"\[ContactID\]").Count;
        int aggregated = Regex.Matches(selects, @"COUNT\(DISTINCT (CASE WHEN .*? THEN )?L\.\[ContactID\]").Count;

        Assert.Multiple(() =>
        {
            Assert.That(references, Is.GreaterThan(0));
            Assert.That(aggregated, Is.EqualTo(references));
            Assert.That(selects, Does.Not.Contain("ActivityContactID"));
        });
    }

    [TestCase(null, null, "All")]
    [TestCase(null, "footer", "All")]
    [TestCase("newsletter", null, "All")]
    [TestCase("newsletter", "", "None")]
    [TestCase("newsletter", "footer", "Value")]
    public void GetContentFilter(string? source, string? content, string expected)
    {
        var query = new CampaignSourcesQuery(new(new(2026, 9, 1), new(2026, 9, 30), StatsGrouping.Day, null), source, content);

        Assert.That(CampaignSourcesRepository.GetContentFilter(query).ToString(), Is.EqualTo(expected));
    }

    private static int Count(string text, string value) =>
        Regex.Matches(text, Regex.Escape(value)).Count;
}

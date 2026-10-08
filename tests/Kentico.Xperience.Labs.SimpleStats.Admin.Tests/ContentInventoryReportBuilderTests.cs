using CMS.ContentEngine;

using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.ContentInventory;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class ContentInventoryReportBuilderTests
{
    private static readonly StatsSnapshotQuery all = new(null, null);

    private static readonly DateTime now = new(2026, 9, 29, 12, 0, 0, DateTimeKind.Unspecified);

    private static readonly ContentTypeRow[] types =
    [
        new(10, "Site.Article", "Article", "Website", 7),
        new(11, "Site.Home", "Home", "Website", 1),
        new(20, "Site.Coffee", "Coffee", "Reusable", 12),
        new(21, "Site.Unused", "Unused", "Reusable", 0),
    ];

    private static readonly ContentLanguageRow[] languages =
    [
        new(2, "es", "Spanish", false, 5),
        new(1, "en", "English", true, 20),
    ];

    [Test]
    public void Build_Empty_ReturnsZeros()
    {
        var result = ContentInventoryReportBuilder.Build(all, ContentInventoryData.Empty, _ => null, _ => null);

        Assert.That(result.TotalItems, Is.Zero);
        Assert.That(result.TotalVariants, Is.Zero);
        Assert.That(result.ContentTypeCount, Is.Zero);
        Assert.That(result.ContentTypesInUse, Is.Zero);
        Assert.That(result.ByContentType.Items, Is.Empty);
        Assert.That(result.ByKind.Items, Is.Empty);
        Assert.That(result.Workflow.Items, Is.Empty);
        Assert.That(result.Workflow.InWorkflow, Is.Zero);
        Assert.That(result.Age.Buckets.Items.Select(i => i.Value), Is.All.Zero);
        Assert.That(result.Age.Oldest, Is.Empty);
        Assert.That(result.UnusedReusable!.Count, Is.Zero);
        Assert.That(result.LanguageCoverage, Is.Empty);
        Assert.That(result.DefaultLanguage, Is.Null);
        Assert.That(result.ByStatus.Items.Select(i => i.Value), Is.All.Zero);
        Assert.That(result.ByStatus.Items.Select(i => i.Share), Is.All.Zero);
    }

    [Test]
    public void Build_ListsEveryContentType_IncludingUnusedLast_WithLinks()
    {
        var result = ContentInventoryReportBuilder.Build(all, new(types, languages, []), id => $"/types/{id}", _ => null);

        Assert.That(result.ByContentType.Items.Select(i => (i.Label, i.SecondaryLabel, i.Value)), Is.EqualTo(new[]
        {
            ("Coffee", "Reusable", 12),
            ("Article", "Website", 7),
            ("Home", "Website", 1),
            ("Unused", "Reusable", 0),
        }));
        Assert.That(result.ByContentType.Items[0].AdminPath, Is.EqualTo("/types/20"));
        Assert.That(result.ByContentType.Items[0].Share, Is.EqualTo(0.6).Within(1e-9));
        Assert.That(result.TotalItems, Is.EqualTo(20));
        Assert.That(result.ContentTypeCount, Is.EqualTo(4));
        Assert.That(result.ContentTypesInUse, Is.EqualTo(3));
    }

    [Test]
    public void Build_GroupsItemsByKind()
    {
        var result = ContentInventoryReportBuilder.Build(all, new(types, languages, []), _ => null, _ => null);

        Assert.That(result.ByKind.Items.Select(i => (i.Key, i.Value, i.SecondaryValue)), Is.EqualTo(new (string, int, int?)[]
        {
            ("Reusable", 12, 2),
            ("Website", 8, 2),
        }));
    }

    [Test]
    public void Build_KeepsAppliedFilter()
    {
        var query = new StatsSnapshotQuery("Website", 2);

        var result = ContentInventoryReportBuilder.Build(query, new(types[..2], languages, []), _ => null, _ => null);

        Assert.That(result.Kind, Is.EqualTo("Website"));
        Assert.That(result.ChannelId, Is.EqualTo(2));
        Assert.That(result.ByContentType.ChannelId, Is.EqualTo(2));
        Assert.That(result.TotalItems, Is.EqualTo(8));
    }

    [Test]
    public void Build_LanguageCoverage_DefaultFirst_WithMissingAndShare()
    {
        var result = ContentInventoryReportBuilder.Build(all, new(types, languages, []), _ => null, _ => null);

        Assert.That(result.LanguageCoverage.Select(c => (c.Key, c.Label, c.Covered, c.Missing, c.Total)), Is.EqualTo(new[]
        {
            ("en", "English", 20, 0, 20),
            ("es", "Spanish", 5, 15, 20),
        }));
        Assert.That(result.LanguageCoverage[1].Share, Is.EqualTo(0.25).Within(1e-9));
        Assert.That(result.DefaultLanguage, Is.EqualTo("English"));
    }

    [Test]
    public void Build_OneLanguage_FullCoverage()
    {
        var result = ContentInventoryReportBuilder.Build(all, new(types, [languages[1]], []), _ => null, _ => null);

        Assert.That(result.LanguageCoverage.Single().Share, Is.EqualTo(1));
        Assert.That(result.LanguageCoverage.Single().Missing, Is.Zero);
    }

    [Test]
    public void Build_Statuses_FixedOrder_WorkflowStepWins()
    {
        ContentStatusRow[] statuses =
        [
            Status(VersionStatus.Published, 10, scheduledUnpublish: 1),
            Status(VersionStatus.InitialDraft, 2, scheduledPublish: 1),
            Status(VersionStatus.Draft, 1),
            Status(VersionStatus.InitialDraft, 3) with { StepId = 5, StepDisplayName = "Review", WorkflowId = 1, WorkflowDisplayName = "Articles" },
            Status(VersionStatus.Draft, 1) with { StepId = 5, StepDisplayName = "Review", WorkflowId = 1, WorkflowDisplayName = "Articles" },
            Status(VersionStatus.Draft, 2) with { StepId = 6, StepDisplayName = "Legal", WorkflowId = 1, WorkflowDisplayName = "Articles" },
        ];

        var result = ContentInventoryReportBuilder.Build(all, new(types, languages, statuses), _ => null, id => $"/workflows/{id}");

        Assert.That(result.ByStatus.Items.Select(i => (i.Key, i.Value)), Is.EqualTo(new[]
        {
            (ContentInventoryReportBuilder.PublishedKey, 10),
            (ContentInventoryReportBuilder.DraftKey, 3),
            (ContentInventoryReportBuilder.WorkflowKey, 6),
            (ContentInventoryReportBuilder.UnpublishedKey, 0),
        }));
        Assert.That(result.TotalVariants, Is.EqualTo(19));
        Assert.That(result.ScheduledPublish, Is.EqualTo(1));
        Assert.That(result.ScheduledUnpublish, Is.EqualTo(1));
        Assert.That(result.Workflow.InWorkflow, Is.EqualTo(6));
    }

    [Test]
    public void Build_Age_FixedBuckets_AndOldestWithDays()
    {
        var data = new ContentInventoryData(types, languages, []) with
        {
            Now = now,
            Age = new(5, 4, 3, 2),
            Oldest = [new(7, "Old article", "Article", "English", now.AddDays(-400).AddHours(-1))],
        };

        var result = ContentInventoryReportBuilder.Build(all, data, _ => null, _ => null);

        Assert.That(result.Age.Buckets.Items.Select(i => (i.Key, i.Value)), Is.EqualTo(new[]
        {
            (ContentInventoryReportBuilder.Under3MonthsKey, 5),
            (ContentInventoryReportBuilder.Months3To6Key, 4),
            (ContentInventoryReportBuilder.Months6To12Key, 3),
            (ContentInventoryReportBuilder.Over12MonthsKey, 2),
        }));
        Assert.That(result.Age.NotModified12Months, Is.EqualTo(2));
        Assert.That(result.Age.Oldest.Single(), Is.EqualTo(new StatsAgedItem("7", "Old article", "Article", "English", null, new(2025, 8, 25), 400)));
    }

    [Test]
    public void Build_Workflow_ItemsWithStepDaysAndLinks()
    {
        var data = new ContentInventoryData(types, languages, [Status(VersionStatus.Draft, 3) with { StepId = 5 }]) with
        {
            Now = now,
            WorkflowItems =
            [
                new(1, "Coffee", "Article", "English", now.AddDays(-30), "Review", 2, "Articles"),
                new(2, "Tea", "Article", "Spanish", now.AddDays(-2), null, null, null),
            ],
            WorkflowOverdue = 1,
        };

        var result = ContentInventoryReportBuilder.Build(all, data, _ => null, id => $"/workflows/{id}");

        Assert.That(result.Workflow.InWorkflow, Is.EqualTo(3));
        Assert.That(result.Workflow.Overdue, Is.EqualTo(1));
        Assert.That(result.Workflow.OverdueDays, Is.EqualTo(ContentInventoryReportBuilder.OverdueDays));
        Assert.That(result.Workflow.Items.Select(i => (i.Label, i.Detail, i.Days, i.AdminPath)), Is.EqualTo(new (string, string?, int, string?)[]
        {
            ("Coffee", "Review (Articles)", 30, "/workflows/2"),
            ("Tea", null, 2, null),
        }));
    }

    [Test]
    public void Build_ForgottenEdits_DraftAndLiveDates_StepAndChannel_SeveralLanguages()
    {
        var link = new ContentItemLink(ContentItemLocation.WebPage, 1, 42, "en");
        var data = new ContentInventoryData(types, languages, []) with
        {
            Now = now,
            PendingDrafts =
            [
                // Over the threshold, live since the last publish.
                new ContentVariantRow(1, "Coffee", "Article", "English", now.AddDays(-30)) { LivePublishedWhen = new(2025, 6, 1, 8, 0, 0, DateTimeKind.Unspecified), Channel = "Site", Link = link },
                // Same item, other language, in a workflow step.
                new ContentVariantRow(2, "Café", "Article", "Spanish", now.AddDays(-20), "Review", 2, "Articles") { LivePublishedWhen = new(2025, 7, 1, 8, 0, 0, DateTimeKind.Unspecified), Channel = "Site" },
                // Within the threshold: pending, not forgotten. Reusable item in the Content hub.
                new ContentVariantRow(3, "Beans", "Coffee", "English", now.AddDays(-2)) { LivePublishedWhen = new(2025, 8, 1, 8, 0, 0, DateTimeKind.Unspecified), IsReusable = true, Workspace = "Marketing" },
            ],
            PendingDraftCount = 3,
            ForgottenEditCount = 2,
        };

        var result = ContentInventoryReportBuilder.Build(all, data, _ => null, id => $"/workflows/{id}", l => $"/item/{l.ObjectId}/{l.LanguageName}");

        var edits = result.ForgottenEdits;
        Assert.That(edits.PendingDrafts, Is.EqualTo(3));
        Assert.That(edits.Forgotten, Is.EqualTo(2));
        Assert.That(edits.OverdueDays, Is.EqualTo(ContentInventoryReportBuilder.OverdueDays));
        Assert.That(edits.Items.Select(i => (i.Key, i.Language, i.Channel, i.Detail, i.Since, i.Days, i.AdminPath)), Is.EqualTo(new (string, string?, string?, string?, DateOnly, int, string?)[]
        {
            ("1", "English", "Site", "Live since 2025-06-01", new(2026, 8, 30), 30, "/item/42/en"),
            ("2", "Spanish", "Site", "Live since 2025-07-01 · Review (Articles)", new(2026, 9, 9), 20, "/workflows/2"),
            ("3", "English", "Content hub - Marketing", "Live since 2025-08-01", new(2026, 9, 27), 2, null),
        }));
    }

    [Test]
    public void Build_ForgottenEdits_CountsCoverMoreThanListed()
    {
        var data = new ContentInventoryData(types, languages, []) with
        {
            Now = now,
            PendingDrafts = [new ContentVariantRow(1, "Coffee", "Article", "English", now.AddDays(-30))],
            PendingDraftCount = 40,
            ForgottenEditCount = 31,
        };

        var edits = ContentInventoryReportBuilder.Build(all, data, _ => null, _ => null).ForgottenEdits;

        Assert.That(edits.PendingDrafts, Is.EqualTo(40));
        Assert.That(edits.Forgotten, Is.EqualTo(31));
        Assert.That(edits.Items.Single().Detail, Is.Null);
    }

    [Test]
    public void Build_ForgottenEdits_EmptyWhenNone()
    {
        var edits = ContentInventoryReportBuilder.Build(all, ContentInventoryData.Empty, _ => null, _ => null).ForgottenEdits;

        Assert.That(edits.PendingDrafts, Is.Zero);
        Assert.That(edits.Forgotten, Is.Zero);
        Assert.That(edits.Items, Is.Empty);
    }

    [Test]
    public void Build_Unused_CountsPerTypeAndListsItems()
    {
        var data = new ContentInventoryData(types, languages, []) with
        {
            Now = now,
            UnusedByContentType = [new(20, "Site.Coffee", "Coffee", "Reusable", 2), new(21, "Site.Unused", "Unused", "Reusable", 1)],
            UnusedItems = [new(9, "Old coffee", "Coffee", now.AddDays(-10)), new(8, "Empty", "Unused", null)],
        };

        var result = ContentInventoryReportBuilder.Build(all, data, id => $"/types/{id}", _ => null);

        var unused = result.UnusedReusable!;
        Assert.That(unused.Count, Is.EqualTo(3));
        Assert.That(unused.ReusableItems, Is.EqualTo(12));
        Assert.That(unused.ByContentType.Items.Select(i => (i.Label, i.Value, i.AdminPath)), Is.EqualTo(new[]
        {
            ("Coffee", 2, "/types/20"),
            ("Unused", 1, "/types/21"),
        }));
        Assert.That(unused.Items.Select(i => (i.Key, i.Category, i.Days)), Is.EqualTo(new (string, string?, int)[]
        {
            ("9", "Coffee", 10),
            ("8", "Unused", 0),
        }));
    }

    [TestCase("Reusable", null, true)]
    [TestCase(null, null, true)]
    [TestCase("Website", null, false)]
    [TestCase("Email", 1, false)]
    public void IncludesReusable_OnlyForAllOrReusableWithoutChannel(string? kind, int? channelId, bool expected) =>
        Assert.That(ContentInventoryReportBuilder.IncludesReusable(new(kind, channelId)), Is.EqualTo(expected));

    [Test]
    public void Build_NotReusable_HasNoUnusedSummary()
    {
        var result = ContentInventoryReportBuilder.Build(new("Website", null), new(types[..2], languages, []), _ => null, _ => null);

        Assert.That(result.UnusedReusable, Is.Null);
    }

    [TestCase("Website", 1, true)]
    [TestCase("Email", 2, true)]
    [TestCase("Headless", 3, true)]
    [TestCase("Website", 2, false)]
    [TestCase("Reusable", 1, false)]
    [TestCase(null, 1, false)]
    [TestCase("Website", 99, false)]
    public void IsChannelAllowed_MatchesChannelTypeToKind(string? kind, int channelId, bool expected)
    {
        StatsChannelOption[] channels = [new(1, "Site", "Website"), new(2, "Emails", "Email"), new(3, "App", "Headless")];

        Assert.That(ContentInventoryReportBuilder.IsChannelAllowed(kind, channelId, channels), Is.EqualTo(expected));
    }

    [Test]
    public void Build_UnknownStatus_IsOther()
    {
        var result = ContentInventoryReportBuilder.Build(all, new([], [], [Status((VersionStatus)42, 2)]), _ => null, _ => null);

        var last = result.ByStatus.Items[^1];
        Assert.That(last.Key, Is.EqualTo(ContentInventoryReportBuilder.OtherKey));
        Assert.That(last.Value, Is.EqualTo(2));
    }

    [TestCase(VersionStatus.Published, ContentInventoryReportBuilder.PublishedKey)]
    [TestCase(VersionStatus.InitialDraft, ContentInventoryReportBuilder.DraftKey)]
    [TestCase(VersionStatus.Draft, ContentInventoryReportBuilder.DraftKey)]
    [TestCase(VersionStatus.Unpublished, ContentInventoryReportBuilder.UnpublishedKey)]
    public void GetStatusKey_MapsVersionStatus(VersionStatus status, string expected)
    {
        Assert.That(ContentInventoryReportBuilder.GetStatusKey((int)status, null), Is.EqualTo(expected));
        Assert.That(ContentInventoryReportBuilder.GetStatusKey((int)status, 1), Is.EqualTo(ContentInventoryReportBuilder.WorkflowKey));
    }

    private static ContentStatusRow Status(VersionStatus status, int count, int scheduledPublish = 0, int scheduledUnpublish = 0) =>
        new((int)status, null, null, null, null, count, scheduledPublish, scheduledUnpublish);
}

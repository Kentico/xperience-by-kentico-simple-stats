using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.EditorContributions;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.PublishingActivity;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class EditorContributionsReportBuilderTests
{
    // Oct 1–7; previous period Sep 24–30.
    private static readonly PublishingActivityQuery query = new(new(new(2026, 10, 1), new(2026, 10, 7), StatsGrouping.Day, null), null);

    private static string UserPath(int userId) => $"/users/{userId}";

    [Test]
    public void Build_DeletedUsers_AreOneUnknownRow_WithoutLink()
    {
        var data = EditorContributionsData.Empty with
        {
            Users =
            [
                new(null, null, false, 2, 1, 0, 1),
                new(null, null, false, 1, 0, 0, 1),
                new(53, "Admin", false, 1, 1, 0, 1),
            ],
            UserCount = 2,
        };

        var result = EditorContributionsReportBuilder.Build(query, data, versionHistoryEnabled: false, versionHistoryLength: 0, UserPath);
        var unknown = result.ByUser.Single(u => u.Key == EditorContributionsReportBuilder.UnknownUserKey);

        Assert.Multiple(() =>
        {
            Assert.That(result.ByUser, Has.Count.EqualTo(2));
            Assert.That(unknown.User, Is.EqualTo(StatsUserLabels.UnknownUserLabel));
            Assert.That(unknown.Created, Is.EqualTo(3));
            Assert.That(unknown.LastModified, Is.EqualTo(1));
            Assert.That(unknown.AdminPath, Is.Null);
            Assert.That(unknown.IsSystemUser, Is.False);
            Assert.That(result.ByUser.Single(u => u.Key == "user:53").AdminPath, Is.EqualTo("/users/53"));
        });
    }

    [Test]
    public void Build_SystemUser_IsItsOwnFlaggedRow()
    {
        var data = EditorContributionsData.Empty with
        {
            Users =
            [
                new(118, "System service user", true, 0, 40, 0, 3),
                new(53, "Admin", false, 5, 2, 0, 2),
            ],
        };

        var result = EditorContributionsReportBuilder.Build(query, data, versionHistoryEnabled: false, versionHistoryLength: 0, UserPath);

        Assert.Multiple(() =>
        {
            Assert.That(result.ByUser.Select(u => u.Key), Is.EqualTo(new[] { "user:118", "user:53" }));
            Assert.That(result.ByUser[0].IsSystemUser, Is.True);
            Assert.That(result.ByUser[0].AdminPath, Is.EqualTo("/users/118"));
            Assert.That(result.ByUser[1].IsSystemUser, Is.False);
        });
    }

    [Test]
    public void Build_RanksByCreatedPlusLastModified_ThenPublished_ThenName()
    {
        var data = EditorContributionsData.Empty with
        {
            Users =
            [
                new(1, "Bea", false, 1, 1, 0, 1),
                new(2, "Ann", false, 0, 2, 0, 1),
                new(3, "Cid", false, 1, 1, 5, 1),
                new(4, "Dan", false, 10, 0, 0, 1),
                // No contributions: left out.
                new(5, "Eve", false, 0, 0, 0, 0),
            ],
            UserCount = 5,
        };

        var result = EditorContributionsReportBuilder.Build(query, data, versionHistoryEnabled: true, versionHistoryLength: 20, UserPath);

        Assert.Multiple(() =>
        {
            Assert.That(result.ByUser.Select(u => u.User), Is.EqualTo(new[] { "Dan", "Cid", "Ann", "Bea" }));
            Assert.That(result.UserCount, Is.EqualTo(5));
        });
    }

    [Test]
    public void Build_CreatedAndModifiedByDifferentUsers_CountForEach()
    {
        // One variant created by user 1 and last modified by user 2.
        var data = EditorContributionsData.Empty with
        {
            Users =
            [
                new(1, "Creator", false, 1, 0, 0, 1),
                new(2, "Editor", false, 0, 1, 0, 1),
            ],
            Totals = new(2, 0, 1, 0, 1, 0, 0, 0),
        };

        var result = EditorContributionsReportBuilder.Build(query, data, versionHistoryEnabled: false, versionHistoryLength: 0, UserPath);

        Assert.Multiple(() =>
        {
            Assert.That(result.ByUser.Single(u => u.User == "Creator").Created, Is.EqualTo(1));
            Assert.That(result.ByUser.Single(u => u.User == "Creator").LastModified, Is.Zero);
            Assert.That(result.ByUser.Single(u => u.User == "Editor").Created, Is.Zero);
            Assert.That(result.ByUser.Single(u => u.User == "Editor").LastModified, Is.EqualTo(1));
            Assert.That(result.ActiveEditors.Current, Is.EqualTo(2));
            Assert.That(result.Created.Current, Is.EqualTo(1));
            Assert.That(result.LastModified.Current, Is.EqualTo(1));
        });
    }

    [Test]
    public void Build_ComparesWithPreviousPeriod()
    {
        var data = EditorContributionsData.Empty with { Totals = new(3, 2, 10, 5, 8, 0, 4, 2) };

        var result = EditorContributionsReportBuilder.Build(query, data, versionHistoryEnabled: true, versionHistoryLength: 20);

        Assert.Multiple(() =>
        {
            Assert.That(result.ActiveEditors.Previous, Is.EqualTo(2));
            Assert.That(result.ActiveEditors.Change, Is.EqualTo(0.5).Within(0.0001));
            Assert.That(result.Created.Change, Is.EqualTo(1).Within(0.0001));
            Assert.That(result.LastModified.Change, Is.Null);
            Assert.That(result.Published?.Current, Is.EqualTo(4));
            Assert.That(result.Created.PreviousFrom, Is.EqualTo(new DateOnly(2026, 9, 24)));
            Assert.That(result.Created.PreviousTo, Is.EqualTo(new DateOnly(2026, 9, 30)));
        });
    }

    [Test]
    public void Build_HistoryDisabled_HasNoPublished()
    {
        var data = EditorContributionsData.Empty with
        {
            Users = [new(53, "Admin", false, 1, 0, 7, 1), new(54, "Publisher", false, 0, 0, 3, 1)],
            Totals = new(1, 0, 1, 0, 0, 0, 7, 0),
        };

        var result = EditorContributionsReportBuilder.Build(query, data, versionHistoryEnabled: false, versionHistoryLength: 20);

        Assert.Multiple(() =>
        {
            Assert.That(result.Published, Is.Null);
            Assert.That(result.VersionHistoryEnabled, Is.False);
            Assert.That(result.ByUser.Select(u => u.Published), Is.All.Null);
            // A user with publishes only has no contributions without version history.
            Assert.That(result.ByUser.Select(u => u.User), Is.EqualTo(new[] { "Admin" }));
        });
    }

    [Test]
    public void Build_Series_TopUsersAndOtherUsers_RangeEdgesIncluded()
    {
        var daily = new List<EditorDailyRow>();
        for (int user = 1; user <= 7; user++)
        {
            daily.Add(new(user, $"User {user}", false, new(2026, 10, 1), 10 - user));
        }
        daily.Add(new(null, null, false, new(2026, 10, 7), 1));
        // Outside the range: ignored.
        daily.Add(new(1, "User 1", false, new(2026, 9, 30), 50));
        daily.Add(new(1, "User 1", false, new(2026, 10, 8), 50));

        var result = EditorContributionsReportBuilder.Build(query, EditorContributionsData.Empty with { Daily = daily }, false, 0);
        var series = result.Series.Series;

        Assert.Multiple(() =>
        {
            Assert.That(series.Select(s => s.Key), Is.EqualTo(new[] { "user:1", "user:2", "user:3", "user:4", "user:5", EditorContributionsReportBuilder.OtherSeries.Key }));
            Assert.That(series[0].DisplayName, Is.EqualTo("User 1"));
            Assert.That(series[0].Total, Is.EqualTo(9));
            // Users 6 (4), 7 (3) and the unknown user (1).
            Assert.That(series[^1].DisplayName, Is.EqualTo("Other users"));
            Assert.That(series[^1].Total, Is.EqualTo(8));
            Assert.That(series[^1].Values[^1], Is.EqualTo(1));
            Assert.That(result.Series.Periods, Has.Count.EqualTo(7));
        });
    }

    [Test]
    public void Build_KeepsFilterAndUserLimit()
    {
        var filtered = query with { Kind = "Website", Range = query.Range with { ChannelId = 2 } };
        var users = Enumerable.Range(1, 30).Select(id => new EditorUserRow(id, $"User {id}", false, id, 0, 0, 1)).ToList();

        var result = EditorContributionsReportBuilder.Build(filtered, EditorContributionsData.Empty with { Users = users, UserCount = 40 }, false, 0);

        Assert.Multiple(() =>
        {
            Assert.That(result.Kind, Is.EqualTo("Website"));
            Assert.That(result.ChannelId, Is.EqualTo(2));
            Assert.That(result.ByUser, Has.Count.EqualTo(EditorContributionsReportBuilder.UserLimit));
            Assert.That(result.ByUser[0].User, Is.EqualTo("User 30"));
            Assert.That(result.UserCount, Is.EqualTo(40));
        });
    }
}

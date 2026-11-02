using System.Reflection;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Labs.SimpleStats.Admin.Reports.Overview;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;
using Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class OverviewPageTests
{
    [Test]
    public void Page_IsRegisteredFirstUnderApplication()
    {
        var registration = typeof(OverviewPage).Assembly
            .GetCustomAttributes<UIPageAttribute>()
            .Single(a => a.Type == typeof(OverviewPage));

        Assert.Multiple(() =>
        {
            Assert.That(registration.ParentType, Is.EqualTo(typeof(StatsApplicationPage)));
            Assert.That(registration.Slug, Is.EqualTo("overview"));
            Assert.That(registration.Name, Is.EqualTo("Overview"));
            Assert.That(registration.Order, Is.EqualTo(50));
            Assert.That(registration.TemplateName, Is.EqualTo("@kentico/xperience-admin-labs-simple-stats/Overview"));
            Assert.That(registration.Icon, Is.EqualTo(Icons.Gauge));
            Assert.That(StatsNavigation.GetChildPages(typeof(StatsApplicationPage))[0].Type, Is.EqualTo(typeof(OverviewPage)));
        });
    }

    [Test]
    public void Page_RequiresOverviewPermission_DeclaredOnApplication()
    {
        var permissions = typeof(StatsApplicationPage)
            .GetCustomAttributes<UIPermissionAttribute>(false)
            .Select(p => (p.Name, p.DisplayName));

        Assert.Multiple(() =>
        {
            Assert.That(typeof(OverviewPage).GetCustomAttribute<UIEvaluatePermissionAttribute>()!.Permission, Is.EqualTo("SimpleStats.Overview"));
            Assert.That(permissions, Does.Contain(("SimpleStats.Overview", "Overview")));
        });
    }

    // The product checks PageCommand.Permission (403 without it) after the page's UIEvaluatePermission (Overview).
    [Test]
    public void EveryTile_HasCommandGuardedBySourceReportPermission()
    {
        var commands = typeof(OverviewPage).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Select(method => method.GetCustomAttribute<PageCommandAttribute>())
            .OfType<PageCommandAttribute>()
            .Where(attribute => attribute.CommandName != "LOG_EXPORT")
            .Select(attribute => (attribute.CommandName, attribute.Permission))
            .ToList();

        Assert.That(commands, Is.EquivalentTo(OverviewTiles.All.Select(tile => (tile.CommandName, tile.Permission))));
    }

    [Test]
    public void EveryTilePermission_IsTheSourceReportPagePermission()
    {
        var reportPermissions = StatsNavigation.GetChildPages(typeof(StatsApplicationPage))
            .Where(section => typeof(StatsSectionPage).IsAssignableFrom(section.Type))
            .SelectMany(section => StatsNavigation.GetChildPages(section.Type))
            .Select(page => page.Type.GetCustomAttribute<UIEvaluatePermissionAttribute>()!.Permission)
            .ToHashSet();

        Assert.That(OverviewTiles.All.Select(t => t.Permission), Is.SubsetOf(reportPermissions));
    }

    [Test]
    public async Task GetVisibleTiles_OnlyTilesWithSourcePermission_OneEvaluationPerTile()
    {
        var evaluator = new FakePermissionEvaluator(StatsPermissions.OVERVIEW, StatsPermissions.FORM_SUBMISSIONS, StatsPermissions.EVENT_LOG);

        var tiles = await OverviewPage.GetVisibleTiles(evaluator);

        Assert.Multiple(() =>
        {
            Assert.That(tiles, Is.EqualTo(new[] { OverviewTiles.Forms, OverviewTiles.Events }));
            Assert.That(evaluator.Evaluated, Is.EqualTo(OverviewTiles.All.Select(t => t.Permission)));
        });
    }

    [Test]
    public async Task GetVisibleTiles_OverviewOnly_NoTiles() =>
        Assert.That(await OverviewPage.GetVisibleTiles(new FakePermissionEvaluator(StatsPermissions.OVERVIEW)), Is.Empty);

    [Test]
    public async Task GetVisibleTiles_AllReports_AllTilesInPageOrder()
    {
        var evaluator = new FakePermissionEvaluator([.. OverviewTiles.All.Select(t => t.Permission)]);

        Assert.That(
            await OverviewPage.GetVisibleTiles(evaluator),
            Is.EqualTo(new[] { "content", "locks", "freshness", "calendar", "emails", "forms", "contacts", "members", "orders", "events", "traffic" }));
    }

    [Test]
    public void TileCommands_TakeRangeOrSnapshotRequest()
    {
        string[] snapshotCommands = [OverviewTiles.LoadContent, OverviewTiles.LoadLocks, OverviewTiles.LoadCalendar];

        Assert.Multiple(() =>
        {
            foreach (var method in typeof(OverviewPage).GetMethods(BindingFlags.Instance | BindingFlags.Public))
            {
                var command = method.GetCustomAttribute<PageCommandAttribute>();
                if (command is null || command.CommandName == "LOG_EXPORT")
                {
                    continue;
                }

                var expected = snapshotCommands.Contains(command.CommandName) ? typeof(StatsSnapshotLoadRequest) : typeof(StatsLoadRequest);
                Assert.That(method.GetParameters()[0].ParameterType, Is.EqualTo(expected), command.CommandName);
                Assert.That(method.ReturnType, Is.EqualTo(typeof(Task<ICommandResponse<OverviewTileResult>>)), command.CommandName);
            }
        });
    }

    private sealed class FakePermissionEvaluator(params string[] granted) : IUIPermissionEvaluator
    {
        public List<string> Evaluated { get; } = [];

        public Task<UIPermissionEvaluationResult> Evaluate(string permission)
        {
            Evaluated.Add(permission);

            return Task.FromResult(new UIPermissionEvaluationResult(granted.Contains(permission)));
        }
    }
}

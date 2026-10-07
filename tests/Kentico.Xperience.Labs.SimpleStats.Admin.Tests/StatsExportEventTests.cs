using System.Reflection;

using CMS.Base;

using Kentico.Xperience.Admin.Base;
using Kentico.Xperience.Labs.SimpleStats.Admin.Shared;
using Kentico.Xperience.Labs.SimpleStats.Admin.UIPages;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kentico.Xperience.Labs.SimpleStats.Admin.Tests;

public class StatsExportEventTests
{
    private static readonly StatsExportLogRequest validRequest = new()
    {
        ExportName = "consents-events",
        FileName = "consents-events_2026-09-01_2026-09-30_day.csv",
        RowCount = 30,
    };

    private FakeClock clock = null!;
    private FakeUserIdAccessor userAccessor = null!;
    private FakeLogger logger = null!;

    [SetUp]
    public void SetUp()
    {
        clock = new FakeClock(new DateTimeOffset(2026, 9, 30, 10, 0, 0, TimeSpan.Zero));
        userAccessor = new FakeUserIdAccessor(42);
        logger = new FakeLogger();
    }

    [Test]
    public async Task Publish_RaisesEventWithData()
    {
        var handler = new RecordingHandler();

        bool published = await CreatePublisher(handler).Publish(typeof(ConsentsPage), validRequest, CancellationToken.None);

        Assert.That(published, Is.True);
        Assert.That(handler.Events, Has.Count.EqualTo(1));
        var data = handler.Events[0].Data;
        Assert.Multiple(() =>
        {
            Assert.That(data.ReportPageTypeName, Is.EqualTo("Kentico.Xperience.Labs.SimpleStats.Admin.UIPages.ConsentsPage"));
            Assert.That(data.UserID, Is.EqualTo(42));
            Assert.That(data.Timestamp, Is.EqualTo(clock.GetLocalNow().DateTime));
            Assert.That(data.ExportName, Is.EqualTo("consents-events"));
            Assert.That(data.FileName, Is.EqualTo("consents-events_2026-09-01_2026-09-30_day.csv"));
            Assert.That(data.RowCount, Is.EqualTo(30));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("Consents-Events")]
    [TestCase("consents_events")]
    [TestCase("consents events")]
    [TestCase("-consents")]
    [TestCase("consents-")]
    [TestCase("consents--events")]
    [TestCase("consents\n")]
    [TestCase("../consents")]
    public async Task Publish_InvalidExportName_Rejected(string? exportName) => await AssertRejected(validRequest with { ExportName = exportName });

    [Test]
    public async Task Publish_TooLongExportName_Rejected() => await AssertRejected(validRequest with { ExportName = new string('a', StatsExportEventPublisher.MAX_EXPORT_NAME_LENGTH + 1) });

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    [TestCase(".")]
    [TestCase("..")]
    [TestCase("../consents.csv")]
    [TestCase("dir/consents.csv")]
    [TestCase("dir\\consents.csv")]
    [TestCase("C:consents.csv")]
    [TestCase("consents?.csv")]
    [TestCase("consents\r\n.csv")]
    public async Task Publish_InvalidFileName_Rejected(string? fileName) => await AssertRejected(validRequest with { FileName = fileName });

    [Test]
    public async Task Publish_TooLongFileName_Rejected() => await AssertRejected(validRequest with { FileName = new string('a', StatsExportEventPublisher.MAX_FILE_NAME_LENGTH - 3) + ".csv" });

    [Test]
    public async Task Publish_NegativeRowCount_Rejected() => await AssertRejected(validRequest with { RowCount = -1 });

    [Test]
    public async Task Publish_NullRequest_Rejected() => await AssertRejected(null);

    [Test]
    public async Task Publish_ZeroRows_Raised()
    {
        var handler = new RecordingHandler();

        await CreatePublisher(handler).Publish(typeof(ConsentsPage), validRequest with { RowCount = 0 }, CancellationToken.None);

        Assert.That(handler.Events.Single().Data.RowCount, Is.Zero);
    }

    [Test]
    public async Task Publish_HandlerThrows_LogsErrorAndRunsOtherHandlers()
    {
        var after = new RecordingHandler();

        bool published = await CreatePublisher(new ThrowingHandler(), after).Publish(typeof(ConsentsPage), validRequest, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(published, Is.True);
            Assert.That(after.Events, Has.Count.EqualTo(1));
            Assert.That(logger.Entries.Single().Level, Is.EqualTo(LogLevel.Error));
            Assert.That(logger.Entries.Single().Exception, Is.TypeOf<InvalidOperationException>());
        });
    }

    [Test]
    public async Task Publish_UserLookupThrows_LogsErrorAndRaisesEventWithUnknownUser()
    {
        var handler = new RecordingHandler();
        var publisher = new StatsExportEventPublisher([handler], new ThrowingUserIdAccessor(), clock, logger);

        bool published = await publisher.Publish(typeof(ConsentsPage), validRequest, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(published, Is.True);
            Assert.That(handler.Events.Single().Data.UserID, Is.Zero);
            Assert.That(logger.Entries.Single().Level, Is.EqualTo(LogLevel.Error));
            Assert.That(logger.Entries.Single().Exception, Is.TypeOf<InvalidOperationException>());
        });
    }

    [Test]
    public async Task Publish_NoHandlers_NoError()
    {
        bool published = await CreatePublisher().Publish(typeof(ConsentsPage), validRequest, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(published, Is.True);
            Assert.That(logger.Entries, Is.Empty);
        });
    }

    [Test]
    public async Task LogExport_PublishesForConcretePageType()
    {
        var publisher = new FakePublisher();
        var page = new TestReportPage(publisher);

        var response = await page.LogExport(validRequest, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(response.Messages, Is.Empty);
            Assert.That(publisher.Calls.Single(), Is.EqualTo((typeof(TestReportPage), validRequest)));
        });
    }

    [Test]
    public void LogExport_RequiresExportPermission()
    {
        var attribute = typeof(StatsReportPage<>)
            .GetMethod(nameof(TestReportPage.LogExport))!
            .GetCustomAttribute<PageCommandAttribute>();

        Assert.Multiple(() =>
        {
            Assert.That(attribute, Is.Not.Null);
            Assert.That(attribute!.CommandName, Is.EqualTo("LOG_EXPORT"));
            Assert.That(attribute.Permission, Is.EqualTo(StatsPermissions.EXPORT));
        });
    }

    // The admin UI tree collects commands with Type.GetMethods(Instance | Public), which includes base class methods.
    // The page's UIEvaluatePermission is checked before any of its commands, so LOG_EXPORT also needs the report permission.
    [Test]
    public void EveryReportPage_HasLogExportCommandAndReportPermission()
    {
        var reportPages = StatsNavigation.GetChildPages(typeof(StatsApplicationPage))
            .Where(section => typeof(StatsSectionPage).IsAssignableFrom(section.Type))
            .SelectMany(section => StatsNavigation.GetChildPages(section.Type))
            .Select(page => page.Type)
            .ToList();

        Assert.That(reportPages, Has.Count.EqualTo(15));
        Assert.Multiple(() =>
        {
            foreach (var type in reportPages)
            {
                var commands = type.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                    .Select(method => method.GetCustomAttribute<PageCommandAttribute>())
                    .Where(attribute => attribute?.CommandName == "LOG_EXPORT")
                    .ToList();
                var reportPermission = type.GetCustomAttribute<UIEvaluatePermissionAttribute>();

                Assert.That(commands, Has.Count.EqualTo(1), type.Name);
                Assert.That(reportPermission, Is.Not.Null, type.Name);
                Assert.That(reportPermission!.Permission, Is.Not.EqualTo(StatsPermissions.EXPORT), type.Name);
            }
        });
    }

    [Test]
    public void AddEventHandler_RegistersHandlerForStatsEvent()
    {
        var services = new ServiceCollection();
        services.AddEventHandler<AfterExportStatsEvent, RecordingHandler>();

        using var provider = services.BuildServiceProvider();

        Assert.That(provider.GetServices<IAsyncEventHandler<AfterExportStatsEvent>>().Single(), Is.TypeOf<RecordingHandler>());
    }

    private async Task AssertRejected(StatsExportLogRequest? request)
    {
        var handler = new RecordingHandler();

        bool published = await CreatePublisher(handler).Publish(typeof(ConsentsPage), request, CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(published, Is.False);
            Assert.That(handler.Events, Is.Empty);
            Assert.That(logger.Entries.Single().Level, Is.EqualTo(LogLevel.Warning));
        });
    }

    private StatsExportEventPublisher CreatePublisher(params IAsyncEventHandler<AfterExportStatsEvent>[] handlers) =>
        new(handlers, userAccessor, clock, logger);

    private sealed class RecordingHandler : IAsyncEventHandler<AfterExportStatsEvent>
    {
        public List<AfterExportStatsEvent> Events { get; } = [];

        public Task HandleAsync(AfterExportStatsEvent asyncEvent, CancellationToken cancellationToken)
        {
            Events.Add(asyncEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingHandler : IAsyncEventHandler<AfterExportStatsEvent>
    {
        public Task HandleAsync(AfterExportStatsEvent asyncEvent, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Handler failed.");
    }

    private sealed class FakeUserIdAccessor(int userId) : IStatsUserIdAccessor
    {
        public Task<int> GetUserId() => Task.FromResult(userId);
    }

    private sealed class ThrowingUserIdAccessor : IStatsUserIdAccessor
    {
        public Task<int> GetUserId() => throw new InvalidOperationException("User lookup failed.");
    }

    private sealed class FakeLogger : ILogger<StatsExportEventPublisher>
    {
        public List<(LogLevel Level, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, exception));
    }

    private sealed class FakePublisher : IStatsExportEventPublisher
    {
        public List<(Type PageType, StatsExportLogRequest? Request)> Calls { get; } = [];

        public Task<bool> Publish(Type reportPageType, StatsExportLogRequest? request, CancellationToken cancellationToken)
        {
            Calls.Add((reportPageType, request));
            return Task.FromResult(true);
        }
    }

    private sealed class AllowAllPermissionEvaluator : IUIPermissionEvaluator
    {
        public Task<UIPermissionEvaluationResult> Evaluate(string permission) => Task.FromResult(new UIPermissionEvaluationResult(true));
    }

    private sealed class TestClientProperties : StatsReportClientProperties
    {
    }

    private sealed class TestReportPage(IStatsExportEventPublisher publisher)
        : StatsReportPage<TestClientProperties>(new AllowAllPermissionEvaluator(), publisher)
    {
        protected override Task<TestClientProperties> ConfigureReportProperties(TestClientProperties properties) =>
            Task.FromResult(properties);
    }
}

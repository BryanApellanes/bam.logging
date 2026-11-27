using System.Diagnostics.Metrics;
using Bam.Logging;
using Bam.Logging.Extensions;
using Bam.Test;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BamLogEventType = Bam.Logging.LogEventType;

namespace Bam.Logging.Tests.Unit;

[UnitTestMenu("BamLoggerProvider should", Selector = "blp")]
public class BamLoggerProviderShould
{
    [UnitTest]
    public void ForwardEntriesWithMappedLevelsCategoryAndScopes()
    {
        When.A<RecordingBamLogger>("forwards MEL entries to a Bam logger",
            () => new RecordingBamLogger(),
            target =>
            {
                using ILoggerFactory factory = LoggerFactory.Create(logging =>
                {
                    logging.SetMinimumLevel(LogLevel.Trace);
                    logging.AddProvider(new BamLoggerProvider(target, LogLevel.Information, ownsTarget: false));
                });
                Microsoft.Extensions.Logging.ILogger logger = factory.CreateLogger("Bam.Logging.Tests.Category");
                using (logger.BeginScope(new Dictionary<string, object?> { ["CorrelationId"] = "corr-9" }))
                {
                    logger.LogInformation("hello {Name}", "world");
                }
                logger.LogDebug("dropped below minimum");
                logger.LogWarning("careful");
                logger.LogError(new InvalidOperationException("boom-message"), "failed {Thing}", "x");
                logger.LogCritical("fatal");
                List<Recorded> entries = target.WaitFor(4).ToList();
                target.StopLoggingThread();
                return new BridgeOutcome(entries);
            })
            .TheTest
            .ShouldPass<BridgeOutcome>((because, outcome) =>
            {
                because.ItsTrue("four entries forwarded (debug dropped)", outcome.Entries.Count == 4);
                because.ItsTrue("category prefixed and message formatted", outcome.Entries[0].Text.StartsWith("[Bam.Logging.Tests.Category] hello world", StringComparison.Ordinal));
                because.ItsTrue("scope appended", outcome.Entries[0].Text.Contains("CorrelationId=corr-9", StringComparison.Ordinal));
                because.ItsTrue("information maps to Information", outcome.Entries[0].Type == BamLogEventType.Information);
                because.ItsTrue("an information entry carries no stack trace", string.IsNullOrWhiteSpace(outcome.Entries[0].StackTrace));
                because.ItsTrue("warning maps to Warning", outcome.Entries[1].Type == BamLogEventType.Warning);
                because.ItsTrue("error maps to Error and carries the exception message", outcome.Entries[2].Type == BamLogEventType.Error && outcome.Entries[2].Message.Contains("boom-message", StringComparison.Ordinal));
                because.ItsTrue("critical maps to Fatal", outcome.Entries[3].Type == BamLogEventType.Fatal);
            })
            .SoBeHappy()
            .UnlessItFailed();
    }

    [UnitTest]
    public void MapEveryLevelAndNeutraliseEveryControlCharacter()
    {
        When.A<string>("maps levels and cleans control characters",
            () => "a\tb\u0001c\r\nd",
            text => new MappingOutcome(
                BamLoggerProvider.Map(LogLevel.Trace),
                BamLoggerProvider.Map(LogLevel.Debug),
                BamLoggerProvider.Map(LogLevel.Information),
                BamLoggerProvider.Map(LogLevel.Warning),
                BamLoggerProvider.Map(LogLevel.Error),
                BamLoggerProvider.Map(LogLevel.Critical),
                BamLoggerProvider.Neutralise(text),
                BamLoggerProvider.Neutralise("clean") == "clean",
                BamLoggerProvider.Compose("Cat", "msg", new EventId(42), null)))
            .TheTest
            .ShouldPass<MappingOutcome>((because, outcome) =>
            {
                because.ItsTrue("trace forwards as Information", outcome.Trace == BamLogEventType.Information);
                because.ItsTrue("debug forwards as Information", outcome.Debug == BamLogEventType.Information);
                because.ItsTrue("information is Information", outcome.Information == BamLogEventType.Information);
                because.ItsTrue("warning is Warning", outcome.Warning == BamLogEventType.Warning);
                because.ItsTrue("error is Error", outcome.Error == BamLogEventType.Error);
                because.ItsTrue("critical is Fatal", outcome.Critical == BamLogEventType.Fatal);
                because.ItsTrue($"tab becomes a space, other controls '?', CR and LF visible ({outcome.Neutralised})", outcome.Neutralised == "a b?c\\r\\nd");
                because.ItsTrue("clean text passes unchanged", outcome.CleanUnchanged);
                because.ItsTrue($"a non-zero event id is appended ({outcome.Composed})", outcome.Composed == "[Cat] msg eventId=42");
            })
            .SoBeHappy()
            .UnlessItFailed();
    }

    [UnitTest]
    public void PassBracesThroughAndNeutraliseLineBreaks()
    {
        When.A<RecordingBamLogger>("forwards awkward text unchanged in meaning",
            () => new RecordingBamLogger(),
            target =>
            {
                using ILoggerFactory factory = LoggerFactory.Create(logging => logging.AddProvider(new BamLoggerProvider(target, LogLevel.Information, ownsTarget: false)));
                Microsoft.Extensions.Logging.ILogger logger = factory.CreateLogger("Cat");
                logger.LogInformation("route {Pattern} body {Json}", "/api/{**catch-all}", "{\"a\":1}");
                logger.LogInformation("path {Path}", "/nope\r\n****************\r\nSource: forged");
                List<Recorded> entries = target.WaitFor(2).ToList();
                target.StopLoggingThread();
                return new TextOutcome(entries[0].Text, entries[1].Text, entries[1].Message);
            })
            .TheTest
            .ShouldPass<TextOutcome>((because, outcome) =>
            {
                because.ItsTrue("braces round-trip unchanged", outcome.Braces == "[Cat] route /api/{**catch-all} body {\"a\":1}");
                because.ItsFalse("no raw CR or LF reaches the Bam entry", outcome.LineBreakText.Contains('\r') || outcome.LineBreakText.Contains('\n'));
                because.ItsTrue("the line breaks are visible instead", outcome.LineBreakText.Contains("\\r\\n****************\\r\\nSource: forged", StringComparison.Ordinal));
                because.ItsFalse("the committed message holds no forged record separator line", outcome.LineBreakMessage.Contains("\n****************", StringComparison.Ordinal));
            })
            .SoBeHappy()
            .UnlessItFailed();
    }

    [UnitTest]
    public void StayOutWhenDisabledAndHonourTheConfiguration()
    {
        string folder = Path.Combine(Path.GetTempPath(), "bam-logging-bridge-" + Guid.NewGuid().ToString("N"));
        When.A<BamLoggingBridgeOptions>("installs the bridge only when enabled and applies its settings",
            () => new BamLoggingBridgeOptions { Enabled = false },
            disabled =>
            {
                ServiceCollection disabledServices = new ServiceCollection();
                disabledServices.AddLogging(logging => logging.AddBamLogging(disabled, "bridge-tests"));
                using ServiceProvider disabledProvider = disabledServices.BuildServiceProvider();
                bool disabledInstalled = disabledProvider.GetServices<Microsoft.Extensions.Logging.ILoggerProvider>().OfType<BamLoggerProvider>().Any();

                BamLoggingBridgeOptions enabled = new BamLoggingBridgeOptions { Target = BamLoggingTarget.TextFile, Folder = folder, MinimumLevel = "Warning", MaxQueueLength = 7 };
                ServiceCollection enabledServices = new ServiceCollection();
                enabledServices.AddLogging(logging => logging.AddBamLogging(enabled, "bridge-tests"));
                ServiceProvider enabledProvider = enabledServices.BuildServiceProvider();
                BamLoggerProvider? bridge = enabledProvider.GetServices<Microsoft.Extensions.Logging.ILoggerProvider>().OfType<BamLoggerProvider>().SingleOrDefault();
                Microsoft.Extensions.Logging.ILogger probe = bridge!.CreateLogger("x");
                bool informationEnabled = probe.IsEnabled(LogLevel.Information);
                bool warningEnabled = probe.IsEnabled(LogLevel.Warning);
                bool textFile = bridge.Target is TextFileLogger;
                int bound = bridge.Target is Logger bounded ? bounded.MaxQueueLength : -1;
                enabledProvider.Dispose();

                Bam.Logging.ILogger both = BamLoggingBuilderExtensions.CreateTarget(new BamLoggingBridgeOptions { Target = BamLoggingTarget.Both, Folder = folder, MaxQueueLength = 9 }, "bridge-tests");
                bool multi = both is MultiTargetLogger { MaxQueueLength: 9 } multiTarget && multiTarget.Loggers.Length == 2;
                both.StopLoggingThread();

                string? badLevel = null;
                try
                {
                    BamLoggingBuilderExtensions.ParseMinimumLevel("Warn");
                }
                catch (InvalidOperationException exception)
                {
                    badLevel = exception.Message;
                }
                return new TargetOutcome(disabledInstalled, bridge is not null, informationEnabled, warningEnabled, textFile, bound, Directory.Exists(folder), multi, badLevel, BamLoggingBuilderExtensions.ParseMinimumLevel(null));
            })
            .TheTest
            .ShouldPass<TargetOutcome>((because, outcome) =>
            {
                because.ItsFalse("disabled installs no Bam provider", outcome.DisabledInstalled);
                because.ItsTrue("enabled (the default) installs exactly one", outcome.EnabledInstalled);
                because.ItsFalse("MinimumLevel Warning filters Information", outcome.InformationEnabled);
                because.ItsTrue("and passes Warning", outcome.WarningEnabled);
                because.ItsTrue("TextFile target is a TextFileLogger", outcome.IsTextFile);
                because.ItsTrue("the queue bound is applied", outcome.Bound == 7);
                because.ItsTrue("folder created", outcome.FolderExists);
                because.ItsTrue("Both is a two-target logger bounded on its own queue", outcome.BothIsMulti);
                because.ItsTrue($"an unknown level name is refused under the default path ({outcome.BadLevel})", outcome.BadLevel?.StartsWith("Logging:Bam:MinimumLevel 'Warn'", StringComparison.Ordinal) == true);
                because.ItsTrue("an empty level means Information", outcome.EmptyLevel == LogLevel.Information);
            })
            .SoBeHappy(_ => { try { Directory.Delete(folder, recursive: true); } catch (IOException) { } })
            .UnlessItFailed();
    }

    [UnitTest]
    public void UseTheHostsMeterInstrumentAndConfigurationPath()
    {
        When.A<BamLoggingBridgeOptions>("takes a host's names for the counter and its configuration",
            () => new BamLoggingBridgeOptions { Target = BamLoggingTarget.Console, MeterName = "Host.Meter", DroppedEventsInstrument = "host_log_events_dropped", ConfigurationPath = "Telemetry:BamLogging" },
            options =>
            {
                string? meterSeen = null;
                using MeterListener listener = new MeterListener();
                listener.InstrumentPublished = (instrument, meterListener) =>
                {
                    if (instrument.Name == "host_log_events_dropped")
                    {
                        meterSeen = instrument.Meter.Name;
                    }
                };
                listener.Start();
                ServiceCollection services = new ServiceCollection();
                services.AddLogging(logging => logging.AddBamLogging(options, "bridge-tests"));
                string? instrumentName;
                using (ServiceProvider provider = services.BuildServiceProvider())
                {
                    BamLoggerProvider bridge = provider.GetServices<Microsoft.Extensions.Logging.ILoggerProvider>().OfType<BamLoggerProvider>().Single();
                    instrumentName = bridge.DroppedEventsInstrument;
                }
                string? levelRefusal = null;
                try
                {
                    BamLoggingBuilderExtensions.ParseMinimumLevel("Loud", options.ConfigurationPath);
                }
                catch (InvalidOperationException exception)
                {
                    levelRefusal = exception.Message;
                }
                string? boundRefusal = null;
                try
                {
                    BamLoggingBuilderExtensions.CreateTarget(new BamLoggingBridgeOptions { Target = BamLoggingTarget.Console, MaxQueueLength = -1, ConfigurationPath = options.ConfigurationPath }, "bridge-tests");
                }
                catch (InvalidOperationException exception)
                {
                    boundRefusal = exception.Message;
                }
                return new NamingOutcome(instrumentName, meterSeen, levelRefusal, boundRefusal);
            })
            .TheTest
            .ShouldPass<NamingOutcome>((because, outcome) =>
            {
                because.ItsTrue("the provider publishes the host's instrument name", outcome.Instrument == "host_log_events_dropped");
                because.ItsTrue($"on the host's meter ({outcome.Meter})", outcome.Meter == "Host.Meter");
                because.ItsTrue($"a level error names the host's key ({outcome.LevelRefusal})", outcome.LevelRefusal?.StartsWith("Telemetry:BamLogging:MinimumLevel 'Loud'", StringComparison.Ordinal) == true);
                because.ItsTrue($"a bound error names the host's key ({outcome.BoundRefusal})", outcome.BoundRefusal?.StartsWith("Telemetry:BamLogging:MaxQueueLength is -1", StringComparison.Ordinal) == true);
            })
            .SoBeHappy()
            .UnlessItFailed();
    }

    [UnitTest]
    public void ReportDroppedEntriesAndRefuseAnUnboundedQueue()
    {
        When.A<BlockedBamLogger>("overflows a small bound behind a blocked sink",
            () => new BlockedBamLogger { MaxQueueLength = 5 },
            target =>
            {
                long observed = 0;
                using MeterListener listener = new MeterListener();
                listener.InstrumentPublished = (instrument, meterListener) =>
                {
                    if (instrument.Name == BamLoggingBridgeOptions.DefaultDroppedEventsInstrument)
                    {
                        meterListener.EnableMeasurementEvents(instrument);
                    }
                };
                // Other tests' providers may still publish 0 under the same name in this process, so keep the largest
                // value seen rather than the last one.
                listener.SetMeasurementEventCallback<long>((instrument, value, tags, state) =>
                {
                    long seen = Interlocked.Read(ref observed);
                    while (value > seen && Interlocked.CompareExchange(ref observed, value, seen) != seen)
                    {
                        seen = Interlocked.Read(ref observed);
                    }
                });
                listener.Start();
                long dropped;
                using (BamLoggerProvider provider = new BamLoggerProvider(target.StartLoggingThread(), ownsTarget: false))
                {
                    Microsoft.Extensions.Logging.ILogger logger = provider.CreateLogger("burst");
                    logger.LogWarning("first");
                    SpinWait.SpinUntil(() => target.Committing, TimeSpan.FromSeconds(5));
                    for (int index = 0; index < 20; index++)
                    {
                        logger.LogWarning("entry {Index}", index);
                    }
                    dropped = provider.DroppedEventCount;
                    listener.RecordObservableInstruments();
                }
                target.Release();
                target.StopLoggingThread();
                string? refusal = null;
                try
                {
                    BamLoggingBuilderExtensions.CreateTarget(new BamLoggingBridgeOptions { Target = BamLoggingTarget.Console, MaxQueueLength = 0 }, "bridge-tests");
                }
                catch (InvalidOperationException exception)
                {
                    refusal = exception.Message;
                }
                return new DropOutcome(dropped, Interlocked.Read(ref observed), refusal);
            })
            .TheTest
            .ShouldPass<DropOutcome>((because, outcome) =>
            {
                because.ItsTrue($"the provider reports the drops ({outcome.Dropped})", outcome.Dropped == 15);
                because.ItsTrue($"bam_log_events_dropped exports them ({outcome.Observed})", outcome.Observed == outcome.Dropped);
                because.ItsTrue("a bound of zero is refused at start-up", outcome.Refusal?.Contains("MaxQueueLength is 0", StringComparison.Ordinal) == true);
            })
            .SoBeHappy()
            .UnlessItFailed();
    }

    [UnitTest]
    public void WriteEveryQueuedEntryWhenTheHostDisposesTheBridge()
    {
        string folder = Path.Combine(Path.GetTempPath(), "bam-logging-drain-" + Guid.NewGuid().ToString("N"));
        When.A<BamLoggingBridgeOptions>("logs through AddBamLogging and disposes the service provider",
            () => new BamLoggingBridgeOptions { Target = BamLoggingTarget.TextFile, Folder = folder, MinimumLevel = "Information" },
            options =>
            {
                ServiceCollection services = new ServiceCollection();
                services.AddLogging(logging => logging.AddBamLogging(options, "bridge-drain"));
                using (ServiceProvider provider = services.BuildServiceProvider())
                {
                    Microsoft.Extensions.Logging.ILogger logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("drain");
                    for (int index = 0; index < 200; index++)
                    {
                        logger.LogInformation("drain-entry-{Index}-end", index);
                    }
                }
                int written = 0;
                foreach (string file in Directory.GetFiles(folder))
                {
                    string text = File.ReadAllText(file);
                    for (int index = 0; index < 200; index++)
                    {
                        if (text.Contains($"drain-entry-{index}-end", StringComparison.Ordinal))
                        {
                            written++;
                        }
                    }
                }
                return written;
            })
            .TheTest
            .ShouldPass<int>((because, written) =>
            {
                because.ItsTrue($"every entry queued before disposal reaches the file ({written} of 200)", written == 200);
            })
            .SoBeHappy(_ => { try { Directory.Delete(folder, recursive: true); } catch (IOException) { } })
            .UnlessItFailed();
    }

    private sealed class BlockedBamLogger : Logger
    {
        private readonly ManualResetEventSlim _gate = new ManualResetEventSlim(false);
        private volatile bool _committing;

        public BlockedBamLogger()
        {
            CommitCycleDelay = 0;
        }

        public bool Committing => _committing;

        public void Release() => _gate.Set();

        public override void CommitLogEvent(LogEvent logEvent)
        {
            _committing = true;
            _gate.Wait();
        }
    }

    private sealed class RecordingBamLogger : Logger
    {
        private readonly List<Recorded> _entries = new List<Recorded>();

        public RecordingBamLogger()
        {
            StartLoggingThread();
        }

        public IReadOnlyList<Recorded> Entries
        {
            get
            {
                lock (_entries)
                {
                    return _entries.ToList();
                }
            }
        }

        /// <summary>Waits (up to five seconds) until the background logging thread has committed at least <paramref name="count"/> entries.</summary>
        public IReadOnlyList<Recorded> WaitFor(int count)
        {
            SpinWait.SpinUntil(() => { lock (_entries) { return _entries.Count >= count; } }, TimeSpan.FromSeconds(5));
            return Entries;
        }

        public override void CommitLogEvent(LogEvent logEvent)
        {
            lock (_entries)
            {
                string text = logEvent.MessageVariableValues is { Length: > 0 } values ? values[0] ?? string.Empty : logEvent.MessageSignature;
                _entries.Add(new Recorded(text, logEvent.Message, logEvent.Severity, logEvent.StackTrace));
            }
        }
    }

    private sealed record Recorded(string Text, string Message, BamLogEventType Type, string? StackTrace);

    private sealed record BridgeOutcome(List<Recorded> Entries);

    private sealed record MappingOutcome(BamLogEventType Trace, BamLogEventType Debug, BamLogEventType Information, BamLogEventType Warning, BamLogEventType Error, BamLogEventType Critical, string Neutralised, bool CleanUnchanged, string Composed);

    private sealed record TextOutcome(string Braces, string LineBreakText, string LineBreakMessage);

    private sealed record DropOutcome(long Dropped, long Observed, string? Refusal);

    private sealed record NamingOutcome(string? Instrument, string? Meter, string? LevelRefusal, string? BoundRefusal);

    private sealed record TargetOutcome(bool DisabledInstalled, bool EnabledInstalled, bool InformationEnabled, bool WarningEnabled, bool IsTextFile, int Bound, bool FolderExists, bool BothIsMulti, string? BadLevel, LogLevel EmptyLevel);
}

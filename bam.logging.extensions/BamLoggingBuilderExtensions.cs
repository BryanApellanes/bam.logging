using Bam.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using BamConsoleLogger = Bam.Console.ConsoleLogger;
using IBamLogger = Bam.Logging.ILogger;

namespace Bam.Logging.Extensions;

/// <summary>Installs the Microsoft.Extensions.Logging to Bam.Logging bridge from <see cref="BamLoggingBridgeOptions"/>.</summary>
public static class BamLoggingBuilderExtensions
{
    /// <summary>
    /// Adds <see cref="BamLoggerProvider"/> over the configured Bam sink when <see cref="BamLoggingBridgeOptions.Enabled"/>
    /// is true; otherwise it does nothing. The options are checked here, eagerly, so a bad value refuses start-up.
    /// </summary>
    /// <param name="logging">The host logging builder.</param>
    /// <param name="options">The bridge options.</param>
    /// <param name="applicationName">The application name Bam stamps on events and uses for the log file name.</param>
    /// <exception cref="InvalidOperationException">The minimum level or the queue bound is invalid.</exception>
    public static ILoggingBuilder AddBamLogging(this ILoggingBuilder logging, BamLoggingBridgeOptions options, string applicationName)
    {
        ArgumentNullException.ThrowIfNull(logging);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);
        if (!options.Enabled)
        {
            return logging;
        }
        LogLevel minimum = ParseMinimumLevel(options.MinimumLevel, options.ConfigurationPath);
        IBamLogger target = CreateTarget(options, applicationName);
        string meterName = options.MeterName;
        string instrument = options.DroppedEventsInstrument;
        // Registered through a factory so the container owns the provider and disposes it with the host. Disposing
        // stops the Bam logging thread, which drains the queue into the sink; an instance passed to AddProvider is
        // never disposed, and whatever was still queued at shutdown would be lost.
        logging.Services.AddSingleton<Microsoft.Extensions.Logging.ILoggerProvider>(_ => new BamLoggerProvider(target, minimum, ownsTarget: true, meterName, instrument));
        return logging;
    }

    /// <summary>Parses a minimum level by name, ignoring case, and refuses anything that isn't a level.</summary>
    /// <param name="value">The configured level name; empty means Information.</param>
    /// <param name="configurationPath">The options' configuration path, named in the error.</param>
    /// <exception cref="InvalidOperationException">The value isn't a <see cref="LogLevel"/> name.</exception>
    public static LogLevel ParseMinimumLevel(string? value, string configurationPath = BamLoggingBridgeOptions.DefaultConfigurationPath)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return LogLevel.Information;
        }
        foreach (LogLevel level in Enum.GetValues<LogLevel>())
        {
            if (string.Equals(level.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                return level;
            }
        }
        throw new InvalidOperationException($"{configurationPath}:MinimumLevel '{value}' is not a log level. Use Trace, Debug, Information, Warning, Error, Critical or None.");
    }

    /// <summary>
    /// Builds and starts the Bam logger for a target: a text file (under the folder, or Bam's default log path), the
    /// console, or both through a <see cref="MultiTargetLogger"/>.
    /// </summary>
    /// <param name="options">The bridge options.</param>
    /// <param name="applicationName">The application name Bam stamps on events and uses for the file name.</param>
    /// <exception cref="InvalidOperationException"><see cref="BamLoggingBridgeOptions.MaxQueueLength"/> is zero or less, which Bam treats as unbounded.</exception>
    public static IBamLogger CreateTarget(BamLoggingBridgeOptions options, string applicationName)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationName);
        if (options.MaxQueueLength <= 0)
        {
            throw new InvalidOperationException($"{options.ConfigurationPath}:MaxQueueLength is {options.MaxQueueLength}; it must be greater than zero (Bam treats zero or less as an unbounded queue).");
        }
        StaticApplicationNameProvider names = new StaticApplicationNameProvider(applicationName);
        IBamLogger logger;
        switch (options.Target)
        {
            case BamLoggingTarget.Console:
                logger = new BamConsoleLogger();
                break;
            case BamLoggingTarget.Both:
                MultiTargetLogger multi = new MultiTargetLogger();
                multi.AddLogger(TextFile(options, names));
                multi.AddLogger(new BamConsoleLogger());
                logger = multi;
                break;
            default:
                logger = TextFile(options, names);
                break;
        }
        logger.ApplicationName = applicationName;
        Bound(logger, options.MaxQueueLength);
        return logger.StartLoggingThread();
    }

    // A MultiTargetLogger is itself a Logger: its own queue holds the entries and it commits to the inner loggers
    // directly, so the bound belongs on the outer logger only.
    private static void Bound(IBamLogger logger, int maxQueueLength)
    {
        if (logger is Logger bounded)
        {
            bounded.MaxQueueLength = maxQueueLength;
        }
    }

    private static TextFileLogger TextFile(BamLoggingBridgeOptions options, StaticApplicationNameProvider names)
    {
        TextFileLogger logger = new TextFileLogger(names);
        if (!string.IsNullOrWhiteSpace(options.Folder))
        {
            string folder = Path.GetFullPath(options.Folder);
            // Log lines can hold user ids, tenant ids and internal addresses: owner-only on Unix.
            if (OperatingSystem.IsWindows())
            {
                Directory.CreateDirectory(folder);
            }
            else
            {
                Directory.CreateDirectory(folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
            logger.Folder = new DirectoryInfo(folder);
        }
        return logger;
    }
}

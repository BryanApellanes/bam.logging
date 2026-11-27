namespace Bam.Logging.Extensions;

/// <summary>The Bam sink that the Microsoft.Extensions.Logging bridge writes to.</summary>
public enum BamLoggingTarget
{
    /// <summary>Bam's <c>TextFileLogger</c> under <see cref="BamLoggingBridgeOptions.Folder"/>.</summary>
    TextFile = 0,

    /// <summary>Bam's coloured <c>ConsoleLogger</c>.</summary>
    Console = 1,

    /// <summary>Both, through a <c>MultiTargetLogger</c>.</summary>
    Both = 2,
}

/// <summary>
/// Configuration for the Microsoft.Extensions.Logging to Bam.Logging bridge. A host usually binds it from a
/// configuration section and sets <see cref="ConfigurationPath"/> to that section's path, so start-up errors name the
/// key the operator has to fix.
/// </summary>
public sealed class BamLoggingBridgeOptions
{
    /// <summary>The meter name used when a host doesn't set <see cref="MeterName"/>.</summary>
    public const string DefaultMeterName = "Bam.Logging";

    /// <summary>The dropped-entries counter name used when a host doesn't set <see cref="DroppedEventsInstrument"/>.</summary>
    public const string DefaultDroppedEventsInstrument = "bam_log_events_dropped";

    /// <summary>The configuration path used in error messages when a host doesn't set <see cref="ConfigurationPath"/>.</summary>
    public const string DefaultConfigurationPath = "Logging:Bam";

    /// <summary>Whether <c>AddBamLogging</c> installs the bridge. Default true; false makes it a no-op.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>The Bam sink. Default <see cref="BamLoggingTarget.TextFile"/>.</summary>
    public BamLoggingTarget Target { get; set; } = BamLoggingTarget.TextFile;

    /// <summary>Folder for the text file logger; empty uses Bam's default log path.</summary>
    public string Folder { get; set; } = string.Empty;

    /// <summary>
    /// The minimum MEL level forwarded, by name. Bam has no Debug or Trace level; entries at those levels are forwarded
    /// as Information when they pass the minimum. Default Information.
    /// </summary>
    public string MinimumLevel { get; set; } = "Information";

    /// <summary>
    /// The most entries the Bam logger holds before dropping new ones, which bounds memory when the sink is slower than
    /// the log rate. Must be greater than zero (Bam treats zero or less as unbounded), or start-up is refused.
    /// Default 10,000.
    /// </summary>
    public int MaxQueueLength { get; set; } = 10000;

    /// <summary>The name of the meter that carries the dropped-entries counter. Default <see cref="DefaultMeterName"/>.</summary>
    public string MeterName { get; set; } = DefaultMeterName;

    /// <summary>The name of the dropped-entries counter. Default <see cref="DefaultDroppedEventsInstrument"/>.</summary>
    public string DroppedEventsInstrument { get; set; } = DefaultDroppedEventsInstrument;

    /// <summary>The configuration path these options were bound from, used in start-up errors. Default <see cref="DefaultConfigurationPath"/>.</summary>
    public string ConfigurationPath { get; set; } = DefaultConfigurationPath;
}

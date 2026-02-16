namespace Bam.Logging
{
    /// <summary>
    /// Represents a log message that is recorded with warning severity.
    /// </summary>
    public class WarningMessage: LogMessage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WarningMessage"/> class.
        /// </summary>
        /// <param name="format">The format string for the warning message.</param>
        /// <param name="args">Optional format arguments for the message.</param>
        public WarningMessage(string format, params string[] args) : base(format, args)
        {
        }

        /// <summary>
        /// Logs this message to the specified logger as a warning entry.
        /// </summary>
        /// <param name="logger">The logger to write the warning entry to.</param>
        public override void Log(ILogger logger)
        {
            logger.AddEntry(Format, LogEventType.Warning, FormatArgs);
        }
    }
}

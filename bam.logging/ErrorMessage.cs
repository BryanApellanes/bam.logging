namespace Bam.Logging
{
    /// <summary>
    /// Represents a log message that includes exception information, logged as an error entry.
    /// </summary>
    public class ErrorMessage: LogMessage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ErrorMessage"/> class.
        /// </summary>
        /// <param name="format">The format string for the log message.</param>
        /// <param name="ex">The exception associated with this error.</param>
        /// <param name="args">Optional format arguments for the message.</param>
        public ErrorMessage(string format, Exception ex, params string[] args)
        {
            Format = format;
            Exception = ex;
            FormatArgs = args;
        }

        /// <summary>
        /// Gets or sets the exception associated with this error message.
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Logs this error message, including the exception, to the specified logger.
        /// </summary>
        /// <param name="logger">The logger to write the error entry to.</param>
        public override void Log(ILogger logger)
        {
            logger.AddEntry(Format, Exception, FormatArgs);
        }
    }
}

namespace Bam.Logging
{
    /// <summary>
    /// Abstract base class that provides access to an <see cref="ILogger"/> and an <see cref="ILogReader"/> via a <see cref="Bam.Logging.LogReaderFactory"/>.
    /// </summary>
    public abstract class LogProvider : ILogProvider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LogProvider"/> class.
        /// </summary>
        /// <param name="defaultLogger">The default logger to use.</param>
        /// <param name="factory">The factory used to create log readers.</param>
        public LogProvider(ILogger defaultLogger, LogReaderFactory factory)
        {
            DefaultLogger = defaultLogger;
            LogReaderFactory = factory;
        }

        protected ILogger DefaultLogger { get; set; }

        /// <summary>
        /// Gets or sets the factory used to create <see cref="ILogReader"/> instances.
        /// </summary>
        public LogReaderFactory LogReaderFactory { get; set; }

        /// <summary>
        /// Gets the logger provided by this log provider.
        /// </summary>
        /// <returns>An <see cref="ILogger"/> instance.</returns>
        public abstract ILogger GetLogger();

        /// <summary>
        /// Creates an <see cref="ILogReader"/> for the logger returned by <see cref="GetLogger"/>.
        /// </summary>
        /// <returns>An <see cref="ILogReader"/> instance.</returns>
        public ILogReader GetLogReader()
        {
            return LogReaderFactory.GetLogReader(GetLogger());
        }
    }
}

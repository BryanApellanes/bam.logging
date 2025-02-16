namespace Bam.Logging
{
    public abstract class LogProvider : ILogProvider
    {
        public LogProvider(ILogger defaultLogger, LogReaderFactory factory)
        {
            DefaultLogger = defaultLogger;
            LogReaderFactory = factory;
        }

        protected ILogger DefaultLogger { get; set; }

        public LogReaderFactory LogReaderFactory { get; set; }

        public abstract ILogger GetLogger();

        public ILogReader GetLogReader()
        {
            return LogReaderFactory.GetLogReader(GetLogger());
        }
    }
}

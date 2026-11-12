/*
	Copyright © Bryan Apellanes 2015  
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Runtime.Versioning;
using Bam.Logging;

namespace Bam.Logging
{
    /// <summary>
    /// A LoggerBase implementation that commits logs to the 
    /// Windows event viewer. The event log exists only on Windows, so the type is marked
    /// <see cref="SupportedOSPlatformAttribute"/> "windows": callers not themselves marked windows-only
    /// get CA1416 at compile time. Loggers created by name through <c>Log.CreateLogger</c> are not checked.
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class WindowsLogger : Logger
    {
        public WindowsLogger()
            : base()
        {
            this.EventIdProvider = new WindowsEventIdProvider();
        }

        public static void CreateLog(string logSource, string logName)
        {
            GetLog(logSource, logName);
        }

        private static EventLog GetLog(string logSource, string logName)
        {
            if (!EventLog.Exists(logName))
            {
                EventLog.CreateEventSource(logSource, logName);
            }

            EventLog eventLog = new EventLog(logName)
            {
                Source = logSource
            };
            eventLog.ModifyOverflowPolicy(OverflowAction.OverwriteAsNeeded, 7);

            return eventLog;
        }

        public override void CommitLogEvent(LogEvent logEvent)
        {
            EventLogEntryType entryType = EventLogEntryType.Information;
            switch (logEvent.Severity)
            {
                case LogEventType.None:
                    entryType = EventLogEntryType.Information;
                    break;
                case LogEventType.Information:
                    entryType = EventLogEntryType.Information;
                    break;
                case LogEventType.Warning:
                    entryType = EventLogEntryType.Warning;
                    break;
                case LogEventType.Error:
                    entryType = EventLogEntryType.Error;
                    break;
                default:
                    break;
            }

            EventLog eventLog = GetLog(ApplicationName, ApplicationName);
            eventLog.WriteEntry($"{logEvent.Message}\r\n\r\n{logEvent.StackTrace}", entryType, 0);
        }
    }
}
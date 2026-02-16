# bam.logging

Extended logging infrastructure with file-based loggers, multi-target output, and log providers.

## Overview

`bam.logging` builds on the core `ILogger` and `Logger` abstractions defined in `bam.base` to provide concrete logging implementations for the BAM framework. The primary implementations are `TextFileLogger`, which writes log events to rotating text files in the user's profile data folder, and `MultiTargetLogger`, which fans out log events to multiple `ILogger` instances in parallel.

The library also introduces the `LogProvider` abstraction for creating loggers and their corresponding log readers, a `LogEventCollection` for persisting collections of log events to XML, and typed log message classes (`ErrorMessage`, `WarningMessage`) that encapsulate formatting and severity for deferred logging. The `LogSchemaType` enum distinguishes between flat and event-relationship-based log storage schemas.

This library depends only on `bam.base` and has no external NuGet package dependencies. It uses `YamlDotNet` and `Newtonsoft.Json` transitively through `bam.base` for configuration support. The `TextFileLogger` automatically rotates log files when they exceed a configurable maximum size (default 1 MB) and names files using the application name from an `IApplicationNameProvider`.

## Key Classes

| Class | Description |
|---|---|
| `TextFileLogger` | Writes log events to rotating text files. Configurable max file size (default 1 MB). Files stored in the BAM profile data folder. |
| `MultiTargetLogger` | Fans out `CommitLogEvent` calls to multiple `ILogger` instances in parallel. Used as the default logger when multiple loggers are registered. |
| `LogProvider` | Abstract base class providing a `GetLogger()` factory pattern paired with a `LogReaderFactory` for reading log entries back. |
| `LogEventCollection` | Serializable `List<LogEvent>` that can be persisted as XML. Provides an `EventLogEntries` array property for serialization. |
| `ErrorMessage` | Log message that captures format string, exception, and args; logs via `logger.AddEntry` with the exception. |
| `WarningMessage` | Log message that logs with `LogEventType.Warning` severity. |
| `LogSchemaType` | Enum: `Invalid`, `Flat`, `EventRelationships` -- distinguishes log storage schema types. |

## Dependencies

### Project References
- `bam.base` -- `ILogger`, `Logger`, `Loggable`, `LogEvent`, `LogEventType`, `VerbosityLevel`, `IApplicationNameProvider`, `DefaultConfiguration`, `BamProfile`, extension methods

### Package References
- None

## Target Framework
- `net10.0`

## Usage Examples

### Creating a text file logger
```csharp
using Bam.Logging;

IApplicationNameProvider nameProvider = new DefaultConfigurationApplicationNameProvider();
TextFileLogger logger = new TextFileLogger(nameProvider);
logger.Folder = new DirectoryInfo("/var/log/myapp");
logger.MaxBytes = 5 * 1024 * 1024; // 5 MB per file

logger.StartLoggingThread();
logger.AddEntry("Application started");
logger.AddEntry("Error occurred: {0}", LogEventType.Error, "something went wrong");
```

### Using MultiTargetLogger with multiple targets
```csharp
using Bam.Logging;

MultiTargetLogger multiLogger = new MultiTargetLogger();
multiLogger.AddLogger(new TextFileLogger(nameProvider));
multiLogger.AddLogger(myCustomDatabaseLogger);
multiLogger.StartLoggingThread();

// All loggers receive the same events
multiLogger.AddEntry("This goes to all targets");
```

### Using typed log messages
```csharp
using Bam.Logging;

ErrorMessage error = new ErrorMessage("Failed to process {0}", exception, "order-123");
error.Log(logger);

WarningMessage warning = new WarningMessage("Retrying operation {0}", "order-123");
warning.Log(logger);
```

### Persisting log events as XML
```csharp
using Bam.Logging;

LogEventCollection collection = new LogEventCollection();
collection.Add(new LogEvent { Message = "Event 1" });
collection.Add(new LogEvent { Message = "Event 2" });

// Serialize to XML using standard XmlSerializer
```

## Known Gaps / Not Yet Implemented

- No known gaps. The library is a focused set of logger implementations with complete functionality.

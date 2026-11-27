# bam.logging.extensions

The bridge from Microsoft.Extensions.Logging (MEL) into Bam loggers, for hosts that log through MEL: ASP.NET Core and the generic host. It's a separate project so the core `bam.logging` library takes no Microsoft.Extensions dependency.

```csharp
BamLoggingBridgeOptions options = builder.Configuration.GetSection("Logging:Bam").Get<BamLoggingBridgeOptions>() ?? new BamLoggingBridgeOptions();
builder.Logging.AddBamLogging(options, "my-host");
```

## What it does
- **One Bam event per MEL entry.** The category is a `[category]` prefix, and scopes and a non-zero event id are appended.
- **Levels:** Critical becomes Fatal, Error stays Error, Warning stays Warning. Everything else becomes Information, because Bam has no Debug or Trace; entries below `MinimumLevel` aren't forwarded.
- **Control characters are neutralised.** CR and LF become a visible `\r` and `\n`, so log text can't forge a record in the text sink.
- **Bounded queue.** `MaxQueueLength` must be greater than zero, or start-up is refused. Drops are counted on the observable counter `DroppedEventsInstrument`, published on meter `MeterName`.
- **Shutdown drains the queue.** The host's container owns the provider, so disposing the host writes out what's still queued.

## Options (`BamLoggingBridgeOptions`)
| Option | Default |
|---|---|
| `Enabled` | `true` |
| `Target` | `TextFile` (or `Console`, `Both`) |
| `Folder` | empty: Bam's default log path |
| `MinimumLevel` | `Information` |
| `MaxQueueLength` | `10000` |
| `MeterName` | `Bam.Logging` |
| `DroppedEventsInstrument` | `bam_log_events_dropped` |
| `ConfigurationPath` | `Logging:Bam`, the path named in start-up errors |

A host that already exports its own metrics sets `MeterName` and `DroppedEventsInstrument` to its own names. bamgateway, for example, uses `Bam.Gateway` and `gateway_log_events_dropped`.

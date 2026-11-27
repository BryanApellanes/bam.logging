using System.Diagnostics.Metrics;
using System.Text;
using Microsoft.Extensions.Logging;
using BamLogEventType = Bam.Logging.LogEventType;
using IBamLogger = Bam.Logging.ILogger;

namespace Bam.Logging.Extensions;

/// <summary>
/// Bridges Microsoft.Extensions.Logging into Bam's logging, so Bam sinks (text file, console, or both through a
/// multi-target logger) receive everything a host, ASP.NET Core and its libraries log. Each MEL entry becomes one Bam
/// log event:
/// <list type="bullet">
/// <item>the level is mapped onto <see cref="BamLogEventType"/>. Bam has no Debug or Trace; they are forwarded as
/// Information when they pass the configured minimum;</item>
/// <item>the message is prefixed with the category, and the active scopes are appended;</item>
/// <item>control characters are neutralised, so log text can't forge a record;</item>
/// <item>an exception is attached.</item>
/// </list>
/// <para>
/// Exception text: <c>bam.base</c> already escapes control characters in an exception's message (outer and inner)
/// when it builds the event; the stack trace is recorded as .NET produces it, one frame per line, which is its format.
/// </para>
/// <para>
/// Scope values are written as <c>key=value</c> without quoting. A value can therefore read like a second pair
/// (<c>User=bob Admin=true</c>), but it can't start a new record. Treat scope text as data, not as parsed fields.
/// </para>
/// </summary>
[ProviderAlias("Bam")]
public sealed class BamLoggerProvider : Microsoft.Extensions.Logging.ILoggerProvider, ISupportExternalScope
{
    /// <summary>The Bam message signature the provider writes with; the formatted MEL text is its only value.</summary>
    public const string MessageSignature = "{0}";

    private readonly IBamLogger _target;
    private readonly Meter _meter;
    private readonly LogLevel _minimum;
    private readonly bool _ownsTarget;
    private IExternalScopeProvider? _scopes;

    /// <summary>Creates the provider over a Bam logger.</summary>
    /// <param name="target">The Bam logger that receives entries.</param>
    /// <param name="minimum">The minimum MEL level forwarded.</param>
    /// <param name="ownsTarget">Whether disposing the provider stops the Bam logger's thread.</param>
    /// <param name="meterName">The meter that carries the dropped-entries counter.</param>
    /// <param name="droppedEventsInstrument">The dropped-entries counter's name.</param>
    public BamLoggerProvider(
        IBamLogger target,
        LogLevel minimum = LogLevel.Information,
        bool ownsTarget = true,
        string meterName = BamLoggingBridgeOptions.DefaultMeterName,
        string droppedEventsInstrument = BamLoggingBridgeOptions.DefaultDroppedEventsInstrument)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(meterName);
        ArgumentException.ThrowIfNullOrWhiteSpace(droppedEventsInstrument);
        _target = target;
        _minimum = minimum;
        _ownsTarget = ownsTarget;
        DroppedEventsInstrument = droppedEventsInstrument;
        // The provider owns its meter, so the counter goes away with it. A host passes its own meter name to export the
        // counter wherever its other metrics go.
        _meter = new Meter(meterName);
        _meter.CreateObservableCounter(droppedEventsInstrument, () => DroppedEventCount, "{event}",
            "Log entries the Bam logging bridge dropped because its queue was full.");
    }

    /// <summary>The name of the dropped-entries counter this provider publishes.</summary>
    public string DroppedEventsInstrument { get; }

    /// <summary>Entries the Bam logger has dropped because its queue was full; zero for a logger without a bound.</summary>
    public long DroppedEventCount => _target is Logger bounded ? bounded.DroppedEventCount : 0;

    /// <summary>The Bam logger entries are forwarded to.</summary>
    public IBamLogger Target => _target;

    /// <inheritdoc />
    public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName)
    {
        return new BridgeLogger(this, categoryName);
    }

    /// <inheritdoc />
    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopes = scopeProvider;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _meter.Dispose();
        if (_ownsTarget)
        {
            _target.StopLoggingThread();
        }
    }

    /// <summary>Maps a MEL level onto Bam's event types.</summary>
    /// <param name="level">The MEL level.</param>
    public static BamLogEventType Map(LogLevel level)
    {
        return level switch
        {
            LogLevel.Critical => BamLogEventType.Fatal,
            LogLevel.Error => BamLogEventType.Error,
            LogLevel.Warning => BamLogEventType.Warning,
            _ => BamLogEventType.Information,
        };
    }

    /// <summary>
    /// Builds the line the bridge hands to Bam: <c>[category] message</c>, the event id, and every scope value.
    /// Control characters are replaced (CR and LF as the visible <c>\r</c> and <c>\n</c>), because Bam's text sink
    /// separates records with lines and every library's log text passes through here; a request path or header
    /// value carrying CR or LF must not be able to write a forged record.
    /// </summary>
    /// <param name="category">The MEL category.</param>
    /// <param name="formatted">The MEL-formatted message.</param>
    /// <param name="eventId">The MEL event id.</param>
    /// <param name="scopes">The scope provider, if any.</param>
    public static string Compose(string category, string formatted, EventId eventId, IExternalScopeProvider? scopes)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentNullException.ThrowIfNull(formatted);
        StringBuilder text = new StringBuilder(formatted.Length + category.Length + 64);
        text.Append('[').Append(category).Append("] ").Append(formatted);
        if (eventId.Id != 0)
        {
            text.Append(" eventId=").Append(eventId.Id);
        }
        scopes?.ForEachScope(static (scope, builder) =>
        {
            if (scope is IEnumerable<KeyValuePair<string, object?>> pairs)
            {
                foreach (KeyValuePair<string, object?> pair in pairs)
                {
                    if (pair.Key != "{OriginalFormat}")
                    {
                        builder.Append(' ').Append(pair.Key).Append('=').Append(pair.Value);
                    }
                }
            }
            else if (scope is not null)
            {
                builder.Append(' ').Append(scope);
            }
        }, text);
        return Neutralise(text.ToString());
    }

    /// <summary>
    /// Replaces characters that could break or disguise a record:
    /// <list type="bullet">
    /// <item>CR and LF become the visible <c>\r</c> and <c>\n</c>;</item>
    /// <item>the Unicode line and paragraph separators (U+2028, U+2029) become <c>\u2028</c> and <c>\u2029</c>, since
    /// some viewers break lines on them;</item>
    /// <item>tab becomes a space;</item>
    /// <item>every other control character, and the bidirectional formatting controls (U+061C, U+200E, U+200F,
    /// U+202A–U+202E, U+2066–U+2069) that can make a line display differently from what it holds, become <c>?</c>.</item>
    /// </list>
    /// </summary>
    /// <param name="value">The text.</param>
    public static string Neutralise(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        StringBuilder? clean = null;
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (!IsUnsafe(character))
            {
                clean?.Append(character);
                continue;
            }
            clean ??= new StringBuilder(value.Length + 8).Append(value, 0, index);
            clean.Append(character switch
            {
                '\r' => "\\r",
                '\n' => "\\n",
                '\u2028' => "\\u2028",
                '\u2029' => "\\u2029",
                '\t' => " ",
                _ => "?",
            });
        }
        return clean?.ToString() ?? value;
    }

    private static bool IsUnsafe(char character)
    {
        return char.IsControl(character)
            || character is '\u2028' or '\u2029' or '\u061C' or '\u200E' or '\u200F'
            || character is >= '\u202A' and <= '\u202E'
            || character is >= '\u2066' and <= '\u2069';
    }

    private void Write(string category, LogLevel level, EventId eventId, string formatted, Exception? exception)
    {
        // Bam formats the signature with string.Format only when values are passed, so the text goes in as the
        // single value of a "{0}" signature: braces in log content arrive unchanged, never doubled or interpreted.
        string line = Compose(category, formatted, eventId, _scopes);
        if (exception is not null)
        {
            _target.AddEntry(MessageSignature, (int)Map(level), exception, line);
        }
        else
        {
            _target.AddEntry(MessageSignature, Map(level), line);
        }
    }

    private sealed class BridgeLogger : Microsoft.Extensions.Logging.ILogger
    {
        private readonly BamLoggerProvider _provider;
        private readonly string _category;

        public BridgeLogger(BamLoggerProvider provider, string category)
        {
            _provider = provider;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return _provider._scopes?.Push(state);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None && logLevel >= _provider._minimum;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }
            ArgumentNullException.ThrowIfNull(formatter);
            // MEL's own providers tolerate a formatter that returns null; so does this one.
            _provider.Write(_category, logLevel, eventId, formatter(state, exception) ?? string.Empty, exception);
        }
    }
}

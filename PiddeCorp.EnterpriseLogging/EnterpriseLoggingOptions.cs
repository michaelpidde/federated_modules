using Serilog.Events;

namespace PiddeCorp.EnterpriseLogging;

/// <summary>
/// Configuration accepted by <see cref="EnterpriseLoggingExtensions.AddEnterpriseLogging"/>.
/// </summary>
public sealed class EnterpriseLoggingOptions {
    /// <summary>
    /// Value attached to every log event as the "Application" property. Defaults to
    /// <c>IHostEnvironment.ApplicationName</c> (the entry assembly name) when left unset.
    /// </summary>
    public string? ServiceName { get; set; }

    /// <summary>
    /// Minimum level for the default logger. Can still be overridden per-category via
    /// a "Serilog" configuration section.
    /// </summary>
    public LogEventLevel MinimumLevel { get; set; } = LogEventLevel.Information;

    /// <summary>
    /// When true (the default), console output is compact single-line JSON suitable for
    /// log aggregators. Set to false for human-readable text output during local dev.
    /// </summary>
    public bool WriteAsJson { get; set; } = true;

    /// <summary>
    /// When true, also forwards log events to any other <c>ILoggerProvider</c> registered
    /// in the host - e.g. Aspire ServiceDefaults' OpenTelemetry logging provider, needed for
    /// the Aspire dashboard to show logs and correlate them with traces. Off by default:
    /// ASP.NET Core registers its own Console/Debug/EventSource providers too, and turning
    /// this on clears those first so they don't duplicate Serilog's own console output -
    /// only enable it for services that deliberately register a provider of their own.
    /// </summary>
    public bool ForwardToProviders { get; set; } = false;
}

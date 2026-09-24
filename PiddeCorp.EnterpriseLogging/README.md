# PiddeCorp.EnterpriseLogging

A shared baseline for logging across PiddeCorp services, built on Serilog. It exists so
that every service configures logging the same way instead of reinventing it per project:

- **Enterprise baseline** — consistent minimum levels, noisy framework categories
  suppressed, and standard enrichers (machine name, environment name, thread ID,
  application name) applied everywhere without copy-pasted setup code.
- **Structured format** — compact JSON console output by default, so logs are
  machine-parseable for aggregation/search rather than ad-hoc text.
- **Developer ergonomics** — two calls and you're done:

```csharp
builder.AddEnterpriseLogging(o => o.ServiceName = "TaskManager.API");
...
app.UseEnterpriseLogging();
```

`AddEnterpriseLogging` also reads an optional `"Serilog"` configuration section, so
individual services can add or override sinks/levels via `appsettings.json` without
touching code.

## Forwarding to other providers (Aspire, OpenTelemetry, etc.)

By default Serilog is the only thing that sees log events - other `ILoggerProvider`s
registered in the host (e.g. Aspire ServiceDefaults' OpenTelemetry logging provider) get
nothing. Set `ForwardToProviders` to opt in:

```csharp
builder.AddEnterpriseLogging(o => {
    o.ServiceName = "TaskManager.API";
    o.ForwardToProviders = true; // e.g. so the Aspire dashboard's Logs pillar gets events
});
```

Turning this on also clears the framework's own default Console/Debug/EventSource
providers first, so they don't produce duplicate console output alongside Serilog's own
Console sink - only providers a service deliberately registers afterward (like
`AddServiceDefaults()`) receive the forwarded events.

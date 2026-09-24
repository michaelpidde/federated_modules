using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Formatting.Compact;

namespace PiddeCorp.EnterpriseLogging;

/// <summary>
/// Entry points for wiring up PiddeCorp's baseline Serilog configuration.
/// </summary>
public static class EnterpriseLoggingExtensions {
    /// <summary>
    /// Wires Serilog up as the logging provider with PiddeCorp's baseline defaults:
    /// structured console output, noisy-framework-category suppression, and a
    /// standard set of enrichers. Call this on the builder, before <c>Build()</c>.
    /// An optional "Serilog" section in configuration can add or override sinks/levels
    /// on top of these defaults.
    /// </summary>
    public static WebApplicationBuilder AddEnterpriseLogging(
        this WebApplicationBuilder builder,
        Action<EnterpriseLoggingOptions>? configureOptions = null
    ) {
        EnterpriseLoggingOptions options = new();
        configureOptions?.Invoke(options);

        if(options.ForwardToProviders) {
            // Otherwise the framework's default Console/Debug/EventSource providers would
            // also receive forwarded events, duplicating Serilog's own Console sink output.
            // Clearing them first means only providers a service deliberately adds
            // afterward - e.g. Aspire ServiceDefaults' OpenTelemetry logging provider - see them.
            builder.Logging.ClearProviders();
        }

        builder.Host.UseSerilog((context, services, loggerConfig) => {
                string serviceName = options.ServiceName ?? context.HostingEnvironment.ApplicationName;

                loggerConfig
                    .MinimumLevel.Is(options.MinimumLevel)
                    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                    .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
                    .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
                    .Enrich.FromLogContext()
                    .Enrich.WithMachineName()
                    .Enrich.WithEnvironmentName()
                    .Enrich.WithThreadId()
                    .Enrich.WithProperty("Application", serviceName)
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services);

                if(options.WriteAsJson) {
                    loggerConfig.WriteTo.Console(new CompactJsonFormatter());
                } else {
                    loggerConfig.WriteTo.Console();
                }
            }, writeToProviders: options.ForwardToProviders
        );

        return builder;
    }

    /// <summary>
    /// Adds Serilog's request-logging middleware, which replaces the framework's
    /// multi-line-per-request logging with a single structured event per request
    /// (method, path, status code, elapsed time). Requires <see cref="AddEnterpriseLogging"/>
    /// to have been called on the builder.
    /// </summary>
    public static IApplicationBuilder UseEnterpriseLogging(this IApplicationBuilder app) {
        return app.UseSerilogRequestLogging();
    }
}

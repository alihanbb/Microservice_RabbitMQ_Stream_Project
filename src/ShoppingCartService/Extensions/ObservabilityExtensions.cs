using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;

namespace ShoppingCartService.Extensions;

public static class ObservabilityExtensions
{
    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        // Serilog: JSON structured logs → Loki via Promtail
        builder.Host.UseSerilog((ctx, cfg) =>
        {
            cfg.ReadFrom.Configuration(ctx.Configuration)
               .Enrich.FromLogContext()
               .Enrich.WithProperty("service", "shopping-cart")
               .Enrich.WithProperty("version", "1.0.0")
               .WriteTo.Console(new CompactJsonFormatter());
        });

        var otlpEndpoint = builder.Configuration["Observability:OtlpEndpoint"] ?? "http://otel-collector:4317";

        // OpenTelemetry: traces + metrics → OTLP Collector
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r
                .AddService("shopping-cart", serviceVersion: "1.0.0")
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = builder.Environment.EnvironmentName
                }))
            .WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation(o => o.RecordException = true)
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddPrometheusExporter()         // /metrics endpoint (Prometheus scrape)
                .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)));

        return builder;
    }

    public static WebApplication UseObservability(this WebApplication app)
    {
        // Expose /metrics for Prometheus scraping
        app.MapPrometheusScrapingEndpoint("/metrics");
        return app;
    }
}

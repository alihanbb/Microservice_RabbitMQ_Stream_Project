using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Compact;

namespace DiscountService.Infrastructure.Configuration;

public static class ObservabilityExtensions
{
    public static WebApplicationBuilder AddObservability(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((ctx, cfg) =>
        {
            cfg.ReadFrom.Configuration(ctx.Configuration)
               .Enrich.FromLogContext()
               .Enrich.WithProperty("service", "discount")
               .Enrich.WithProperty("version", "1.0.0")
               .WriteTo.Console(new CompactJsonFormatter());
        });

        var otlpEndpoint = builder.Configuration["Observability:OtlpEndpoint"] ?? "http://otel-collector:4317";

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r
                .AddService("discount", serviceVersion: "1.0.0")
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
                .AddRuntimeInstrumentation()
                .AddPrometheusExporter()
                .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint)));

        return builder;
    }

    public static WebApplication UseObservability(this WebApplication app)
    {
        app.MapPrometheusScrapingEndpoint("/metrics");
        return app;
    }
}

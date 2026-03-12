using Bff.Gateway;

var builder = WebApplication.CreateBuilder(args);

builder.AddObservability();  // Serilog + OpenTelemetry + Prometheus
builder.Services.AddHealthChecks();
builder.Services.GatewayService(builder.Configuration);

var app = builder.Build();

app.UseGatewayEndpoints();
app.MapPrometheusScrapingEndpoint("/metrics");  // /metrics endpoint
app.MapHealthChecks("/health");
app.MapReverseProxy();
app.UseRouting();
app.GatewayApp();
app.Run();

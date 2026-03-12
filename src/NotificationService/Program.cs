using NotificationService.Application.Interfaces;
using NotificationService.Application.Services;
using NotificationService.Infrastructure.Configuration;
using NotificationService.Infrastructure.Messaging;
using Serilog;
using Serilog.Formatting.Compact;

// Serilog: structured JSON logging → Loki via Promtail
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithProperty("service", "notification")
    .Enrich.WithProperty("version", "1.0.0")
    .WriteTo.Console(new CompactJsonFormatter())
    .CreateLogger();

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog(Log.Logger);  // wire Serilog as ILogger provider
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<INotificationSender, LogNotificationSender>();
builder.Services.AddHostedService<CartNotificationConsumer>();
builder.Services.AddObservabilityServices(builder.Configuration);  // OTel traces + metrics

var host = builder.Build();
await host.RunAsync();

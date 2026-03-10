using NotificationService.Application.Interfaces;
using NotificationService.Application.Services;
using NotificationService.Infrastructure.Messaging;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<INotificationSender, LogNotificationSender>();
builder.Services.AddHostedService<CartNotificationConsumer>();

var host = builder.Build();
await host.RunAsync();

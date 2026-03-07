using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShoppingCartService.Application.Interfaces;

namespace ShoppingCartService.Infrastructure.Messaging;

/// <summary>
/// Hosted service that initializes the RabbitMQ Stream Publisher on application startup.
/// This ensures the publisher is ready before any background tasks or requests are processed.
/// </summary>
public class RabbitMQInitializationService : IHostedService
{
    private readonly IRabbitMQStreamPublisher _publisher;
    private readonly ILogger<RabbitMQInitializationService> _logger;

    public RabbitMQInitializationService(
        IRabbitMQStreamPublisher publisher,
        ILogger<RabbitMQInitializationService> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting RabbitMQ Stream Publisher initialization");
            
            if (_publisher is RabbitMQStreamPublisher streamPublisher)
            {
                await streamPublisher.InitializeAsync(cancellationToken);
                _logger.LogInformation("RabbitMQ Stream Publisher initialized successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize RabbitMQ Stream Publisher during startup");
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Stopping RabbitMQ Stream Publisher");
            
            if (_publisher is RabbitMQStreamPublisher streamPublisher)
            {
                await streamPublisher.DisposeAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error during RabbitMQ Stream Publisher shutdown");
        }
    }
}

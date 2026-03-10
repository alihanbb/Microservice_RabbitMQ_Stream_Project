namespace ShoppingCartService.Infrastructure.Messaging;


public class RabbitMQInitializationService(
        IRabbitMQStreamPublisher publisher,
        ILogger<RabbitMQInitializationService> logger) : IHostedService
{
   
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Starting RabbitMQ Super Stream Publisher initialization");
            
            if (publisher is RabbitMQStreamPublisher streamPublisher)
            {
                await streamPublisher.InitializeAsync(cancellationToken);
                logger.LogInformation("RabbitMQ Super Stream Publisher initialized successfully");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize RabbitMQ Super Stream Publisher during startup");
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Stopping RabbitMQ Super Stream Publisher");
            
            if (publisher is RabbitMQStreamPublisher streamPublisher)
                await streamPublisher.DisposeAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error during RabbitMQ Super Stream Publisher shutdown");
        }
    }
}

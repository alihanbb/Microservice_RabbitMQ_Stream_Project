namespace ShoppingCartService.Infrastructure.Messaging;


public class RabbitMQInitializationService(
        IRabbitMQStreamPublisher publisher,
        ILogger<RabbitMQInitializationService> logger) : IHostedService
{
    private const int MaxRetries = 10;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting RabbitMQ Super Stream Publisher initialization");

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                if (publisher is RabbitMQStreamPublisher streamPublisher)
                    await streamPublisher.InitializeAsync(cancellationToken);

                logger.LogInformation("RabbitMQ Super Stream Publisher initialized successfully");
                return;
            }
            catch (Exception ex) when (attempt < MaxRetries && !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex,
                    "RabbitMQ not ready (attempt {Attempt}/{Max}), retrying in {Delay}s...",
                    attempt, MaxRetries, RetryDelay.TotalSeconds);

                await Task.Delay(RetryDelay, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to initialize RabbitMQ Super Stream Publisher after {Max} attempts", MaxRetries);
                throw;
            }
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

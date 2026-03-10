using System.Net;
using System.Text;
using System.Text.Json;
using RabbitMQ.Stream.Client;
using RabbitMQ.Stream.Client.AMQP;
using RabbitMQ.Stream.Client.Reliable;

namespace DiscountService.Infrastructure.Messaging;

public sealed class CartEventConsumer(
        ILogger<CartEventConsumer> logger,
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory) : BackgroundService
{

    private StreamSystem? _streamSystem;
    private Consumer? _consumer;
    private Producer? _dlqProducer;

   

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var host = configuration["RabbitMQ:Host"];
        var port = int.TryParse(configuration["RabbitMQ:Port"], out var p) ? p : 5552;
        var username = configuration["RabbitMQ:Username"];
        var password = configuration["RabbitMQ:Password"];
        var streamName = configuration["RabbitMQ:StreamName"];
        var partitions = int.TryParse(configuration["RabbitMQ:Partitions"], out var parts) ? parts : 3;
        var retryCount = 0;
        const int maxRetries = 10;

        while (!stoppingToken.IsCancellationRequested && retryCount < maxRetries)
        {
            try
            {
                logger.LogInformation("Connecting to RabbitMQ Super Stream at {Host}:{Port}...", host, port);

                var config = new StreamSystemConfig
                {
                    UserName = username,
                    Password = password,
                    Endpoints = [new DnsEndPoint(host, port)],
                    RpcTimeOut = TimeSpan.FromSeconds(30),
                    Heartbeat = TimeSpan.FromSeconds(60)
                };

                _streamSystem = await StreamSystem.Create(config);
                logger.LogInformation("Connected to RabbitMQ Stream system");

               
                try
                {
                    await _streamSystem.CreateSuperStream(new PartitionsSuperStreamSpec(streamName, partitions));
                    logger.LogInformation("Super stream '{StreamName}' created with {Partitions} partitions", streamName, partitions);
                }
                catch (CreateStreamException)
                {
                    logger.LogDebug("Super stream '{StreamName}' already exists", streamName);
                }

                // Create DLQ Stream
                var dlqStreamName = "shopping-cart-dlq";
                try
                {
                    await _streamSystem.CreateStream(new StreamSpec(dlqStreamName) { MaxLengthBytes = 5L * 1024 * 1024 * 1024 });
                    logger.LogInformation("DLQ stream '{StreamName}' created", dlqStreamName);
                }
                catch (CreateStreamException)
                {
                    logger.LogDebug("DLQ stream '{StreamName}' already exists", dlqStreamName);
                }

                _dlqProducer = await Producer.Create(new ProducerConfig(_streamSystem, dlqStreamName));

                // Subscribe to all partitions of the super stream
                _consumer = await Consumer.Create(new ConsumerConfig(_streamSystem, streamName)
                {
                    IsSuperStream = true,
                    OffsetSpec = new OffsetTypeNext(),
                    MessageHandler = async (stream, _, _, message) =>
                    {
                        await ProcessMessageAsync(message, stream);
                    }
                });

                logger.LogInformation("RabbitMQ Super Stream consumer started on '{StreamName}' ({Partitions} partitions)", streamName, partitions);

                // Keep alive until cancelled
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("RabbitMQ Super Stream consumer stopping due to cancellation");
                break;
            }
            catch (Exception ex)
            {
                retryCount++;
                var delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, retryCount)));

                logger.LogWarning(ex,
                    "Failed to connect to RabbitMQ Super Stream (attempt {Attempt}/{MaxRetries}). Retrying in {Delay}s...",
                    retryCount, maxRetries, delay.TotalSeconds);

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        if (retryCount >= maxRetries)
        {
            logger.LogError("Failed to connect to RabbitMQ Super Stream after {MaxRetries} attempts. Consumer will not start.", maxRetries);
        }
    }

    private async Task ProcessMessageAsync(Message message, string partitionName)
    {
        int retryCount = 0;
        Exception? lastException = null;

        while (retryCount < 3)
        {
            try
            {
                var contents = message.Data.Contents;
                var json = Encoding.UTF8.GetString(
                    contents.IsSingleSegment
                        ? contents.First.Span
                        : System.Buffers.BuffersExtensions.ToArray(contents));
                var cartEvent = JsonSerializer.Deserialize<CartConfirmedEvent>(json);

                if (cartEvent == null)
                {
                    logger.LogWarning("Received null cart event from super stream partition '{Partition}'", partitionName);
                    return;
                }

                logger.LogInformation(
                    "Cart confirmed event received — CartId: {CartId}, TotalAmount: {TotalAmount:C}, Items: {ItemCount}, ConfirmedAt: {ConfirmedAt}, Partition: {Partition}",
                    cartEvent.CartId,
                    cartEvent.TotalAmount,
                    cartEvent.TotalItems,
                    cartEvent.ConfirmedAt,
                    partitionName);

                // Process in a new scope (for DI-scoped services like DbContext)
                using var scope = scopeFactory.CreateScope();
                var couponRepository = scope.ServiceProvider.GetRequiredService<Domain.Repositories.ICouponCodeRepository>();

                // Log analytics: check if there are valid coupons that could apply to this cart
                var validCoupons = await couponRepository.GetValidCouponsAsync();
                var applicableCoupons = validCoupons
                    .Where(c => c.CalculateTotalDiscount(cartEvent.TotalAmount) > 0)
                    .ToList();

                if (applicableCoupons.Count > 0)
                {
                    var bestCoupon = applicableCoupons
                        .OrderByDescending(c => c.CalculateTotalDiscount(cartEvent.TotalAmount))
                        .First();

                    var maxDiscount = bestCoupon.CalculateTotalDiscount(cartEvent.TotalAmount);

                    logger.LogInformation(
                        "Analytics: Cart {CartId} (${TotalAmount:F2}) had {Count} applicable coupons. Best: {BestCoupon} (${Discount:F2} off)",
                        cartEvent.CartId,
                        cartEvent.TotalAmount,
                        applicableCoupons.Count,
                        bestCoupon.Code,
                        maxDiscount);
                }
                else
                {
                    logger.LogInformation(
                        "Analytics: Cart {CartId} (${TotalAmount:F2}) confirmed without applicable coupons",
                        cartEvent.CartId,
                        cartEvent.TotalAmount);
                }

                // Success
                return;
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "Failed to deserialize cart event from partition '{Partition}'", partitionName);
                lastException = ex;
                break; // Unrecoverable parsing error, don't retry, move to DLQ
            }
            catch (Exception ex)
            {
                lastException = ex;
                retryCount++;
                logger.LogWarning(ex, "Error processing event from partition '{Partition}'. Retry {RetryCount}/3", partitionName, retryCount);
                if (retryCount < 3)
                {
                    await Task.Delay(1000 * retryCount);
                }
            }
        }

        // Move to DLQ
        logger.LogError("Message {MessageId} failed processing. Moving to DLQ. Reason: {Error}", message.Properties?.MessageId, lastException?.Message);

        if (_dlqProducer != null)
        {
            try
            {
                var dlqBytes = message.Data.Contents.IsSingleSegment
                    ? message.Data.Contents.First.Span.ToArray()
                    : System.Buffers.BuffersExtensions.ToArray(message.Data.Contents);

                var dlqMessage = new Message(dlqBytes)
                {
                    Properties = new Properties { MessageId = message.Properties?.MessageId ?? Guid.NewGuid().ToString() },
                    ApplicationProperties = new ApplicationProperties
                    {
                        { "ErrorReason", lastException?.Message ?? "Unknown Error" },
                        { "FailedAt", DateTime.UtcNow.ToString("O") }
                    }
                };
                
                await _dlqProducer.Send(dlqMessage);
                logger.LogInformation("Message {MessageId} successfully moved to DLQ", dlqMessage.Properties.MessageId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to publish message {MessageId} to DLQ!", message.Properties?.MessageId);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_consumer != null)
            {
                await _consumer.Close();
                _consumer = null;
            }

            if (_dlqProducer != null)
            {
                await _dlqProducer.Close();
                _dlqProducer = null;
            }

            if (_streamSystem != null)
            {
                await _streamSystem.Close();
                _streamSystem = null;
            }

            logger.LogInformation("RabbitMQ Super Stream consumer stopped");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Error while stopping RabbitMQ Super Stream consumer");
        }

        await base.StopAsync(cancellationToken);
    }
}
